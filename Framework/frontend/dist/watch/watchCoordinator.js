import path from 'node:path';
import { performance } from 'node:perf_hooks';
import { context as createEsbuildContext } from 'esbuild';
import { FOLDERS, FILES, FILE_NAMES, EXTENSIONS } from '../core/constants.js';
import { getPages } from '../core/pages.js';
import { emitDiagnostic } from '../core/diagnostics.js';
import { prepareWorkspaceConfig } from '../config/setup.js';
import { ensureDir } from '../utils/fs.js';
import { shouldProcess, isPathInside } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';
import { createCssBuilder } from '../builders/cssBuilder.js';
import { createHtmlBuilder } from '../builders/htmlBuilder.js';
import { createStaticAssetsBuilder } from '../builders/staticAssetsBuilder.js';
import { WatchReporter, serializeMessages } from './watchReporter.js';
import { HotUpdateTracker } from './hotUpdateTracker.js';
import { runBuilderWithDiagnostics, emitPipelineSuccess, serializeSummary, emitJavaScriptFailure, JavaScriptBuildError } from './pipelineHelpers.js';
import { resolveEntryPoint, copyRefreshScript } from './frontendFiles.js';
const JAVASCRIPT_EXTENSIONS = [EXTENSIONS.ts, EXTENSIONS.js, '.tsx', '.jsx'];
export class WatchCoordinator {
    workspaceRoot;
    jsContexts = new Map();
    verbose;
    hmrVerbose;
    reporter;
    hotUpdateTracker;
    hmrTotals = { hotUpdates: 0, reloadFallbacks: 0 };
    config;
    isStopping = false;
    queue = Promise.resolve();
    constructor(options) {
        this.workspaceRoot = options.workspaceRoot;
        this.verbose = options.verbose ?? false;
        this.hmrVerbose = options.hmrVerbose ?? false;
        this.reporter = new WatchReporter({ verbose: this.verbose });
        this.hotUpdateTracker = new HotUpdateTracker({ workspaceRoot: this.workspaceRoot });
    }
    async start() {
        if (this.config) {
            return;
        }
        this.reporter.emitVerbose({
            code: 'frontend.watch.starting',
            kind: 'watch-daemon',
            stage: 'startup',
            severity: 'info',
            message: 'Starting frontend watch daemon...'
        });
        this.config = await prepareWorkspaceConfig(this.workspaceRoot);
        await this.refreshJavaScriptContexts();
        const pipelineReady = await this.runFullBuildCycle();
        if (pipelineReady) {
            this.reporter.emitVerbose({
                code: 'frontend.watch.ready',
                kind: 'watch-daemon',
                stage: 'startup',
                severity: 'info',
                message: 'Frontend watch daemon is ready.'
            });
        }
    }
    async reload() {
        await this.enqueue(async () => {
            if (!this.config) {
                await this.start();
                return;
            }
            this.reporter.emitVerbose({
                code: 'frontend.watch.reload',
                kind: 'watch-daemon',
                stage: 'startup',
                severity: 'info',
                message: 'Reloading frontend watch contexts...'
            });
            await this.refreshJavaScriptContexts();
            const pipelineSucceeded = await this.runFullBuildCycle();
            if (pipelineSucceeded) {
                this.reporter.emitVerbose({
                    code: 'frontend.watch.reload.complete',
                    kind: 'watch-daemon',
                    stage: 'startup',
                    severity: 'info',
                    message: 'Frontend watch contexts reloaded.'
                });
            }
        });
    }
    async handleChange(intent) {
        await this.enqueue(async () => {
            if (!this.config) {
                await this.start();
            }
            const resolvedChange = this.resolveChangedFile(intent.path);
            await this.runFullBuildCycle(resolvedChange);
        });
    }
    async stop() {
        if (this.isStopping) {
            return;
        }
        this.isStopping = true;
        await this.enqueue(async () => {
            for (const entry of this.jsContexts.values()) {
                await entry.context.dispose();
            }
            this.jsContexts.clear();
            this.hotUpdateTracker.reset();
            this.config = undefined;
        });
        this.isStopping = false;
        this.reporter.emitVerbose({
            code: 'frontend.watch.stopped',
            kind: 'watch-daemon',
            stage: 'shutdown',
            severity: 'info',
            message: 'Frontend watch daemon stopped.'
        });
    }
    async enqueue(task) {
        const runTask = async () => {
            try {
                await task();
            }
            catch (error) {
                this.logUnexpectedError('queue-task', error);
            }
        };
        this.queue = this.queue.then(runTask, runTask);
        await this.queue;
    }
    async refreshJavaScriptContexts() {
        const config = this.requireConfig();
        const pages = await getPages(config.paths.src.pages);
        const observed = new Set();
        for (const page of pages) {
            observed.add(page.name);
            await this.ensureJavaScriptContext(config, page);
        }
        for (const existing of Array.from(this.jsContexts.keys())) {
            if (!observed.has(existing)) {
                const context = this.jsContexts.get(existing);
                if (context) {
                    await context.context.dispose();
                }
                this.jsContexts.delete(existing);
                this.hotUpdateTracker.removePage(existing);
                this.reporter.emitVerbose({
                    code: 'frontend.watch.javascript.context.removed',
                    kind: 'watch-daemon',
                    stage: 'javascript',
                    severity: 'info',
                    message: `Removed watch context for page '${existing}'.`
                });
            }
        }
    }
    async ensureJavaScriptContext(config, page) {
        const entryPoint = await resolveEntryPoint(page.directory);
        if (!entryPoint) {
            emitDiagnostic({
                code: 'frontend.watch.javascript.entry.missing',
                kind: 'watch-daemon',
                stage: 'javascript',
                severity: 'warning',
                message: `No JavaScript entry point found for page '${page.name}'.`
            });
            if (this.jsContexts.has(page.name)) {
                const existing = this.jsContexts.get(page.name);
                if (existing) {
                    await existing.context.dispose();
                }
                this.jsContexts.delete(page.name);
                this.hotUpdateTracker.removePage(page.name);
            }
            return;
        }
        const existing = this.jsContexts.get(page.name);
        if (existing && path.resolve(existing.entryPoint) === path.resolve(entryPoint)) {
            return;
        }
        if (existing) {
            await existing.context.dispose();
            this.jsContexts.delete(page.name);
            this.hotUpdateTracker.removePage(page.name);
        }
        const outputDir = path.join(config.paths.build.frontend, FOLDERS.pages, page.name);
        await ensureDir(outputDir);
        const context = await createEsbuildContext({
            entryPoints: [entryPoint],
            bundle: true,
            format: 'esm',
            target: 'es2020',
            platform: 'browser',
            sourcemap: true,
            outfile: path.join(outputDir, `${FILES.index}${EXTENSIONS.js}`),
            logLevel: 'silent',
            metafile: true
        });
        this.jsContexts.set(page.name, {
            name: page.name,
            entryPoint,
            context
        });
        this.reporter.emitVerbose({
            code: 'frontend.watch.javascript.context.created',
            kind: 'watch-daemon',
            stage: 'javascript',
            severity: 'info',
            message: `Created watch context for page '${page.name}'.`
        });
    }
    async runFullBuildCycle(changedFile) {
        const summary = await this.runJavaScriptBuild(changedFile);
        if (!summary) {
            return false;
        }
        const assetsResult = await this.runAdditionalBuilders(changedFile);
        if (!assetsResult.succeeded) {
            return false;
        }
        const requiresReload = !changedFile || summary.requiresReload || assetsResult.requiresReload;
        const fallbackReasons = this.combineFallbackReasons(summary.fallbackReasons, assetsResult.fallbackReasons);
        const relativeChange = this.getRelativeChange(changedFile);
        const baseHotUpdate = {
            modules: summary.modules,
            styles: assetsResult.styles,
            requiresReload,
            fallbackReasons,
            changedFile
        };
        const stats = this.recordHotUpdateOutcome(changedFile, relativeChange, baseHotUpdate);
        const hotUpdate = stats
            ? {
                ...baseHotUpdate,
                stats
            }
            : baseHotUpdate;
        if (changedFile && requiresReload) {
            this.emitHotUpdateFallback(relativeChange ?? changedFile, hotUpdate);
        }
        emitPipelineSuccess(summary, assetsResult, changedFile, relativeChange, hotUpdate);
        return true;
    }
    async runAdditionalBuilders(changedFile) {
        const config = this.requireConfig();
        const context = { config, changedFile };
        const builders = [
            createCssBuilder(context),
            createHtmlBuilder(context),
            createStaticAssetsBuilder(context)
        ];
        const executed = [];
        const styles = [];
        let succeeded = true;
        let requiresReload = false;
        const pageNames = Array.from(this.jsContexts.keys());
        const relativeChange = this.getRelativeChange(changedFile);
        const fallbackReasons = [];
        const normalizedChange = changedFile ? path.resolve(changedFile) : undefined;
        const appTemplatePath = path.resolve(config.paths.src.app, FILE_NAMES.htmlAppTemplate);
        const isHtmlChange = Boolean(normalizedChange && ((path.extname(normalizedChange).toLowerCase() === EXTENSIONS.html
            && (isPathInside(normalizedChange, config.paths.src.pages) || isPathInside(normalizedChange, config.paths.src.app)))
            || normalizedChange === appTemplatePath));
        const staticAssetDirectories = [
            config.paths.src.images,
            config.paths.src.fonts,
            config.paths.src.media
        ].filter((directory) => Boolean(directory)).map((directory) => path.resolve(directory));
        const robotsPath = path.resolve(config.paths.src.frontend, FILES.robotsTxt);
        const isStaticAssetChange = Boolean(normalizedChange && (staticAssetDirectories.some(directory => isPathInside(normalizedChange, directory))
            || normalizedChange === robotsPath));
        for (const builder of builders) {
            executed.push(builder.name);
            const builderSucceeded = await runBuilderWithDiagnostics(builder, this.reporter, context, changedFile, relativeChange);
            if (!builderSucceeded) {
                succeeded = false;
                break;
            }
            if (builder.name === 'css') {
                const cssResult = await this.hotUpdateTracker.collectCssChanges(context, pageNames);
                styles.push(...cssResult.styles);
                if (cssResult.requiresReload) {
                    requiresReload = true;
                }
                fallbackReasons.push(...cssResult.fallbackReasons);
            }
            if (builder.name === 'html') {
                if (!changedFile || isHtmlChange) {
                    requiresReload = true;
                    fallbackReasons.push('builder.html.reload');
                }
            }
            if (builder.name === 'static-assets') {
                if (!changedFile || isStaticAssetChange) {
                    requiresReload = true;
                    fallbackReasons.push('builder.static-assets.reload');
                }
            }
        }
        return {
            succeeded,
            assets: executed,
            styles,
            requiresReload,
            fallbackReasons: this.combineFallbackReasons([], fallbackReasons)
        };
    }
    getRelativeChange(changedFile) {
        if (!changedFile) {
            return undefined;
        }
        return path.relative(this.workspaceRoot, changedFile);
    }
    async runJavaScriptBuild(changedFile) {
        const config = this.requireConfig();
        const context = { config, changedFile };
        const shouldRun = shouldProcess(context, [
            {
                directory: config.paths.src.frontend,
                extensions: JAVASCRIPT_EXTENSIONS
            },
            {
                directory: config.paths.src.pages,
                extensions: JAVASCRIPT_EXTENSIONS
            }
        ]);
        const relativeChange = this.getRelativeChange(changedFile);
        if (shouldRun) {
            this.reporter.emitVerbose({
                code: 'frontend.watch.javascript.build.start',
                kind: 'watch-daemon',
                stage: 'javascript',
                severity: 'info',
                message: `Starting JavaScript rebuild${relativeChange ? ` (${relativeChange})` : ''}.`,
                data: changedFile ? { changedFile } : undefined
            });
        }
        try {
            const summary = shouldRun
                ? await this.executeJavaScriptBuild(changedFile)
                : { pagesBuilt: [], warnings: [], modules: [], requiresReload: false, fallbackReasons: [] };
            const skipped = !shouldRun;
            const message = skipped
                ? `JavaScript rebuild not required${relativeChange ? ` (${relativeChange})` : ''}.`
                : `JavaScript rebuild completed (${summary.pagesBuilt.length} page(s))${relativeChange ? ` (${relativeChange})` : ''}.`;
            this.reporter.emitVerbose({
                code: 'frontend.watch.javascript.build.success',
                kind: 'watch-daemon',
                stage: 'javascript',
                severity: 'info',
                message,
                data: serializeSummary(summary, changedFile, skipped)
            });
            return summary;
        }
        catch (error) {
            emitJavaScriptFailure(error, changedFile);
            return null;
        }
    }
    async executeJavaScriptBuild(changedFile) {
        const config = this.requireConfig();
        const targetPages = this.resolveTargetPages(changedFile);
        if (targetPages.length === 0) {
            return { pagesBuilt: [], warnings: [], modules: [], requiresReload: false, fallbackReasons: [] };
        }
        const warnings = [];
        const builtPages = [];
        const modules = [];
        let requiresReload = false;
        const fallbackReasons = [];
        for (const pageName of targetPages) {
            const pageContext = this.jsContexts.get(pageName);
            if (!pageContext) {
                continue;
            }
            try {
                const start = performance.now();
                const result = await pageContext.context.rebuild();
                const duration = performance.now() - start;
                builtPages.push(pageName);
                warnings.push(...serializeMessages(result.warnings ?? []));
                this.reporter.emitJavaScriptStats(pageName, result, duration);
                const outputDetails = await this.hotUpdateTracker.processJavaScriptResult(pageName, result, config);
                modules.push(...outputDetails.modules);
                if (outputDetails.requiresReload) {
                    requiresReload = true;
                }
                fallbackReasons.push(...outputDetails.fallbackReasons);
            }
            catch (error) {
                throw new JavaScriptBuildError(pageName, error);
            }
        }
        if (builtPages.length > 0) {
            await copyRefreshScript(this.requireConfig());
        }
        return {
            pagesBuilt: builtPages,
            warnings,
            modules,
            requiresReload,
            fallbackReasons: this.combineFallbackReasons([], fallbackReasons)
        };
    }
    resolveTargetPages(changedFile) {
        if (!changedFile) {
            return Array.from(this.jsContexts.keys());
        }
        const config = this.requireConfig();
        const targetPage = findPageFromChangedFile(changedFile, config.paths.src.pages);
        if (targetPage && this.jsContexts.has(targetPage)) {
            return [targetPage];
        }
        return Array.from(this.jsContexts.keys());
    }
    resolveChangedFile(changedFile) {
        if (!changedFile) {
            return undefined;
        }
        if (path.isAbsolute(changedFile)) {
            return changedFile;
        }
        return path.resolve(this.workspaceRoot, changedFile);
    }
    emitHotUpdateFallback(changedFile, hotUpdate) {
        if (hotUpdate.fallbackReasons.length === 0) {
            return;
        }
        emitDiagnostic({
            code: 'frontend.watch.pipeline.hmrfallback',
            kind: 'watch-daemon',
            stage: 'pipeline',
            severity: 'info',
            message: `Hot update fallback triggered for '${changedFile}' (${hotUpdate.fallbackReasons.join(', ')}).`,
            data: {
                changedFile,
                reasons: hotUpdate.fallbackReasons,
                modules: hotUpdate.modules.map(asset => asset.url),
                styles: hotUpdate.styles.map(asset => asset.url)
            }
        });
    }
    recordHotUpdateOutcome(changedFile, relativeChange, hotUpdate) {
        if (!changedFile) {
            return undefined;
        }
        if (hotUpdate.requiresReload) {
            this.hmrTotals.reloadFallbacks += 1;
        }
        else {
            this.hmrTotals.hotUpdates += 1;
        }
        const snapshot = {
            hotUpdates: this.hmrTotals.hotUpdates,
            reloadFallbacks: this.hmrTotals.reloadFallbacks
        };
        if (hotUpdate.requiresReload && hotUpdate.fallbackReasons.length > 0) {
            this.reporter.emitVerbose({
                code: 'frontend.watch.hmr.fallback.detail',
                kind: 'watch-daemon',
                stage: 'pipeline',
                severity: 'info',
                message: `Hot update declined for '${relativeChange ?? changedFile}'.`,
                data: {
                    changedFile: relativeChange ?? changedFile,
                    fallbackReasons: hotUpdate.fallbackReasons
                }
            });
        }
        if (this.hmrVerbose) {
            const identifier = relativeChange ?? changedFile;
            const modules = hotUpdate.modules.map(asset => asset.relativePath);
            const styles = hotUpdate.styles.map(asset => asset.relativePath);
            emitDiagnostic({
                code: 'frontend.watch.hmr.summary',
                kind: 'watch-daemon',
                stage: 'pipeline',
                severity: 'info',
                message: hotUpdate.requiresReload
                    ? `HMR fallback required for '${identifier}'.`
                    : `Hot update applied for '${identifier}'.`,
                data: {
                    changedFile: identifier,
                    requiresReload: hotUpdate.requiresReload,
                    fallbackReasons: hotUpdate.fallbackReasons,
                    modules,
                    styles,
                    totals: snapshot
                }
            });
        }
        return snapshot;
    }
    combineFallbackReasons(first, second) {
        return Array.from(new Set([...first, ...second].filter(Boolean)));
    }
    requireConfig() {
        if (!this.config) {
            throw new Error('Watch coordinator not initialized.');
        }
        return this.config;
    }
    logUnexpectedError(stage, error) {
        const message = error instanceof Error ? error.message : String(error);
        emitDiagnostic({
            code: 'frontend.watch.unexpected',
            kind: 'watch-daemon',
            stage,
            severity: 'error',
            message: `Unexpected watch daemon error: ${message}`
        });
    }
}

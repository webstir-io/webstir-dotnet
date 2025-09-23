import path from 'node:path';
import { performance } from 'node:perf_hooks';
import { context as createEsbuildContext } from 'esbuild';
import { FOLDERS, FILES, EXTENSIONS } from '../core/constants.js';
import { getPages } from '../core/pages.js';
import { emitDiagnostic } from '../core/diagnostics.js';
import { prepareWorkspaceConfig } from '../config/setup.js';
import { ensureDir, pathExists, copy } from '../utils/fs.js';
import { shouldProcess } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';
import { createCssBuilder } from '../builders/cssBuilder.js';
import { createHtmlBuilder } from '../builders/htmlBuilder.js';
import { createStaticAssetsBuilder } from '../builders/staticAssetsBuilder.js';
import { WatchReporter, serializeMessages } from './watchReporter.js';
const JAVASCRIPT_EXTENSIONS = [EXTENSIONS.ts, EXTENSIONS.js, '.tsx', '.jsx'];
const BUILDER_DISPLAY_NAMES = {
    css: 'CSS',
    html: 'HTML',
    'static-assets': 'Static assets'
};
export class WatchCoordinator {
    workspaceRoot;
    jsContexts = new Map();
    verbose;
    reporter;
    config;
    isStopping = false;
    queue = Promise.resolve();
    constructor(options) {
        this.workspaceRoot = options.workspaceRoot;
        this.verbose = options.verbose ?? false;
        this.reporter = new WatchReporter({ verbose: this.verbose });
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
            metafile: this.verbose
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
        this.emitPipelineSuccess(summary, assetsResult, changedFile);
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
        let succeeded = true;
        for (const builder of builders) {
            executed.push(builder.name);
            const builderSucceeded = await this.runBuilderWithDiagnostics(builder, context, changedFile);
            if (!builderSucceeded) {
                succeeded = false;
                break;
            }
        }
        return {
            succeeded,
            assets: executed
        };
    }
    async runBuilderWithDiagnostics(builder, context, changedFile) {
        const displayName = BUILDER_DISPLAY_NAMES[builder.name] ?? builder.name;
        const relativeChange = this.getRelativeChange(changedFile);
        const messageContext = relativeChange ? ` (${relativeChange})` : '';
        this.reporter.emitVerbose({
            code: `frontend.watch.${builder.name}.build.start`,
            kind: 'watch-daemon',
            stage: builder.name,
            severity: 'info',
            message: `Starting ${displayName} rebuild${messageContext}.`,
            data: changedFile ? { changedFile, builder: builder.name } : { builder: builder.name }
        });
        try {
            await builder.build(context);
            this.reporter.emitVerbose({
                code: `frontend.watch.${builder.name}.build.success`,
                kind: 'watch-daemon',
                stage: builder.name,
                severity: 'info',
                message: `${displayName} rebuild completed${messageContext}.`,
                data: changedFile ? { changedFile, builder: builder.name } : { builder: builder.name }
            });
            return true;
        }
        catch (error) {
            const details = { builder: builder.name };
            if (changedFile) {
                details.changedFile = changedFile;
            }
            if (error instanceof Error) {
                details.error = error.message;
            }
            else {
                details.error = String(error);
            }
            emitDiagnostic({
                code: `frontend.watch.${builder.name}.build.failure`,
                kind: 'watch-daemon',
                stage: builder.name,
                severity: 'error',
                message: `${displayName} rebuild failed${messageContext}.`,
                data: details
            });
            return false;
        }
    }
    emitPipelineSuccess(summary, assetsResult, changedFile) {
        const relativeChange = this.getRelativeChange(changedFile);
        const message = `Frontend rebuild pipeline completed${relativeChange ? ` (${relativeChange})` : ''}.`;
        const data = {
            pages: summary.pagesBuilt,
            assets: assetsResult.assets
        };
        if (changedFile) {
            data.changedFile = changedFile;
        }
        if (summary.warnings.length > 0) {
            data.javascriptWarnings = summary.warnings;
        }
        emitDiagnostic({
            code: 'frontend.watch.pipeline.success',
            kind: 'watch-daemon',
            stage: 'pipeline',
            severity: 'info',
            message,
            data
        });
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
            const summary = shouldRun ? await this.executeJavaScriptBuild(changedFile) : { pagesBuilt: [], warnings: [] };
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
                data: this.serializeSummary(summary, changedFile, skipped)
            });
            return summary;
        }
        catch (error) {
            this.emitJavaScriptFailure(error, changedFile);
            return null;
        }
    }
    async executeJavaScriptBuild(changedFile) {
        const targetPages = this.resolveTargetPages(changedFile);
        if (targetPages.length === 0) {
            return { pagesBuilt: [], warnings: [] };
        }
        const warnings = [];
        const builtPages = [];
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
            }
            catch (error) {
                throw new JavaScriptBuildError(pageName, error);
            }
        }
        if (builtPages.length > 0) {
            await copyRefreshScript(this.requireConfig());
        }
        return { pagesBuilt: builtPages, warnings };
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
    serializeSummary(summary, changedFile, skipped = false) {
        const data = {
            pages: summary.pagesBuilt
        };
        if (changedFile) {
            data.changedFile = changedFile;
        }
        if (summary.warnings.length > 0) {
            data.warnings = summary.warnings;
        }
        if (skipped) {
            data.skipped = true;
        }
        return data;
    }
    emitJavaScriptFailure(error, changedFile) {
        let message = 'JavaScript rebuild failed.';
        let severity = 'error';
        const data = changedFile ? { changedFile } : {};
        if (error instanceof JavaScriptBuildError) {
            message = `JavaScript rebuild failed for page '${error.pageName}'.`;
            if (error.details.length > 0) {
                data.errors = error.details;
            }
        }
        else if (error instanceof Error) {
            message = `JavaScript rebuild failed: ${error.message}`;
        }
        emitDiagnostic({
            code: 'frontend.watch.javascript.build.failure',
            kind: 'watch-daemon',
            stage: 'javascript',
            severity,
            message,
            data: Object.keys(data).length > 0 ? data : undefined
        });
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
class JavaScriptBuildError extends Error {
    pageName;
    details;
    constructor(pageName, cause) {
        const message = cause instanceof Error ? cause.message : String(cause);
        super(message);
        this.pageName = pageName;
        this.details = isBuildFailure(cause) ? serializeMessages(cause.errors ?? []) : [];
    }
}
async function resolveEntryPoint(pageDirectory) {
    const candidates = [`${FILES.index}${EXTENSIONS.ts}`, `${FILES.index}.tsx`, `${FILES.index}${EXTENSIONS.js}`, `${FILES.index}.jsx`];
    for (const candidate of candidates) {
        const file = path.join(pageDirectory, candidate);
        if (await pathExists(file)) {
            return file;
        }
    }
    return null;
}
async function copyRefreshScript(config) {
    const source = path.join(config.paths.src.app, FILES.refreshJs);
    if (!(await pathExists(source))) {
        return;
    }
    const destination = path.join(config.paths.build.frontend, FILES.refreshJs);
    await ensureDir(path.dirname(destination));
    await copy(source, destination);
}
function isBuildFailure(error) {
    if (typeof error !== 'object' || error === null) {
        return false;
    }
    return Array.isArray(error.errors);
}

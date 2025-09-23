import path from 'node:path';
import { context as createEsbuildContext } from 'esbuild';
import type { BuildContext, BuildFailure, Message } from 'esbuild';
import { FOLDERS, FILES, EXTENSIONS } from '../core/constants.js';
import { getPages, type PageInfo } from '../core/pages.js';
import { emitDiagnostic } from '../core/diagnostics.js';
import type { DiagnosticSeverity } from '../core/diagnostics.js';
import type { FrontendConfig } from '../types.js';
import { prepareWorkspaceConfig } from '../config/setup.js';
import { ensureDir, pathExists, copy } from '../utils/fs.js';
import { shouldProcess } from '../utils/changedFile.js';
import { findPageFromChangedFile } from '../utils/pathMatch.js';
import { createCssBuilder } from '../builders/cssBuilder.js';
import { createHtmlBuilder } from '../builders/htmlBuilder.js';
import { createStaticAssetsBuilder } from '../builders/staticAssetsBuilder.js';
import type { Builder, BuilderContext } from '../builders/types.js';
import type { WatchChangeIntent, WatchCoordinatorOptions } from './types.js';

interface PageBuildContext {
    readonly name: string;
    readonly entryPoint: string;
    readonly context: BuildContext;
}

interface JavaScriptBuildSummary {
    readonly pagesBuilt: readonly string[];
    readonly warnings: readonly SerializedMessage[];
}

interface AdditionalBuildResult {
    readonly succeeded: boolean;
    readonly assets: readonly string[];
}

interface SerializedMessage {
    readonly text: string;
    readonly location?: {
        readonly file?: string;
        readonly line?: number;
        readonly column?: number;
    };
}

const JAVASCRIPT_EXTENSIONS = [EXTENSIONS.ts, EXTENSIONS.js, '.tsx', '.jsx'] as const;

const BUILDER_DISPLAY_NAMES: Record<string, string> = {
    css: 'CSS',
    html: 'HTML',
    'static-assets': 'Static assets'
} as const;

export class WatchCoordinator {
    private readonly workspaceRoot: string;
    private readonly jsContexts = new Map<string, PageBuildContext>();
    private config?: FrontendConfig;
    private isStopping = false;
    private queue: Promise<void> = Promise.resolve();

    public constructor(options: WatchCoordinatorOptions) {
        this.workspaceRoot = options.workspaceRoot;
    }

    public async start(): Promise<void> {
        if (this.config) {
            return;
        }

        emitDiagnostic({
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
            emitDiagnostic({
                code: 'frontend.watch.ready',
                kind: 'watch-daemon',
                stage: 'startup',
                severity: 'info',
                message: 'Frontend watch daemon is ready.'
            });
        }
    }

    public async reload(): Promise<void> {
        await this.enqueue(async () => {
            if (!this.config) {
                await this.start();
                return;
            }

            emitDiagnostic({
                code: 'frontend.watch.reload',
                kind: 'watch-daemon',
                stage: 'startup',
                severity: 'info',
                message: 'Reloading frontend watch contexts...'
            });

            await this.refreshJavaScriptContexts();
            const pipelineSucceeded = await this.runFullBuildCycle();

            if (pipelineSucceeded) {
                emitDiagnostic({
                    code: 'frontend.watch.reload.complete',
                    kind: 'watch-daemon',
                    stage: 'startup',
                    severity: 'info',
                    message: 'Frontend watch contexts reloaded.'
                });
            }
        });
    }

    public async handleChange(intent: WatchChangeIntent): Promise<void> {
        await this.enqueue(async () => {
            if (!this.config) {
                await this.start();
            }

            const resolvedChange = this.resolveChangedFile(intent.path);
            await this.runFullBuildCycle(resolvedChange);
        });
    }

    public async stop(): Promise<void> {
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

        emitDiagnostic({
            code: 'frontend.watch.stopped',
            kind: 'watch-daemon',
            stage: 'shutdown',
            severity: 'info',
            message: 'Frontend watch daemon stopped.'
        });
    }

    private async enqueue(task: () => Promise<void>): Promise<void> {
        const runTask = async () => {
            try {
                await task();
            } catch (error) {
                this.logUnexpectedError('queue-task', error);
            }
        };

        this.queue = this.queue.then(runTask, runTask);
        await this.queue;
    }

    private async refreshJavaScriptContexts(): Promise<void> {
        const config = this.requireConfig();
        const pages = await getPages(config.paths.src.pages);
        const observed = new Set<string>();

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
                emitDiagnostic({
                    code: 'frontend.watch.javascript.context.removed',
                    kind: 'watch-daemon',
                    stage: 'javascript',
                    severity: 'info',
                    message: `Removed watch context for page '${existing}'.`
                });
            }
        }
    }

    private async ensureJavaScriptContext(config: FrontendConfig, page: PageInfo): Promise<void> {
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
            logLevel: 'silent'
        });

        this.jsContexts.set(page.name, {
            name: page.name,
            entryPoint,
            context
        });

        emitDiagnostic({
            code: 'frontend.watch.javascript.context.created',
            kind: 'watch-daemon',
            stage: 'javascript',
            severity: 'info',
            message: `Created watch context for page '${page.name}'.`
        });
    }

    private async runFullBuildCycle(changedFile?: string): Promise<boolean> {
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

    private async runAdditionalBuilders(changedFile?: string): Promise<AdditionalBuildResult> {
        const config = this.requireConfig();
        const context: BuilderContext = { config, changedFile };
        const builders: Builder[] = [
            createCssBuilder(context),
            createHtmlBuilder(context),
            createStaticAssetsBuilder(context)
        ];

        const executed: string[] = [];
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

    private async runBuilderWithDiagnostics(builder: Builder, context: BuilderContext, changedFile?: string): Promise<boolean> {
        const displayName = BUILDER_DISPLAY_NAMES[builder.name] ?? builder.name;
        const relativeChange = this.getRelativeChange(changedFile);
        const messageContext = relativeChange ? ` (${relativeChange})` : '';

        emitDiagnostic({
            code: `frontend.watch.${builder.name}.build.start`,
            kind: 'watch-daemon',
            stage: builder.name,
            severity: 'info',
            message: `Starting ${displayName} rebuild${messageContext}.`,
            data: changedFile ? { changedFile, builder: builder.name } : { builder: builder.name }
        });

        try {
            await builder.build(context);
            emitDiagnostic({
                code: `frontend.watch.${builder.name}.build.success`,
                kind: 'watch-daemon',
                stage: builder.name,
                severity: 'info',
                message: `${displayName} rebuild completed${messageContext}.`,
                data: changedFile ? { changedFile, builder: builder.name } : { builder: builder.name }
            });
            return true;
        } catch (error) {
            const details: Record<string, unknown> = { builder: builder.name };
            if (changedFile) {
                details.changedFile = changedFile;
            }
            if (error instanceof Error) {
                details.error = error.message;
            } else {
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

    private emitPipelineSuccess(summary: JavaScriptBuildSummary, assetsResult: AdditionalBuildResult, changedFile?: string): void {
        const relativeChange = this.getRelativeChange(changedFile);
        const message = `Frontend rebuild pipeline completed${relativeChange ? ` (${relativeChange})` : ''}.`;

        const data: Record<string, unknown> = {
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

    private getRelativeChange(changedFile?: string): string | undefined {
        if (!changedFile) {
            return undefined;
        }

        return path.relative(this.workspaceRoot, changedFile);
    }

    private async runJavaScriptBuild(changedFile?: string): Promise<JavaScriptBuildSummary | null> {
        const config = this.requireConfig();
        const context: BuilderContext = { config, changedFile };
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
            emitDiagnostic({
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

            emitDiagnostic({
                code: 'frontend.watch.javascript.build.success',
                kind: 'watch-daemon',
                stage: 'javascript',
                severity: 'info',
                message,
                data: this.serializeSummary(summary, changedFile, skipped)
            });

            return summary;
        } catch (error) {
            this.emitJavaScriptFailure(error, changedFile);
            return null;
        }
    }

    private async executeJavaScriptBuild(changedFile?: string): Promise<JavaScriptBuildSummary> {
        const targetPages = this.resolveTargetPages(changedFile);
        if (targetPages.length === 0) {
            return { pagesBuilt: [], warnings: [] };
        }

        const warnings: SerializedMessage[] = [];
        const builtPages: string[] = [];
        for (const pageName of targetPages) {
            const pageContext = this.jsContexts.get(pageName);
            if (!pageContext) {
                continue;
            }

            try {
                const result = await pageContext.context.rebuild();
                builtPages.push(pageName);
                warnings.push(...serializeMessages(result.warnings ?? []));
            } catch (error) {
                throw new JavaScriptBuildError(pageName, error);
            }
        }

        if (builtPages.length > 0) {
            await copyRefreshScript(this.requireConfig());
        }

        return { pagesBuilt: builtPages, warnings };
    }

    private resolveTargetPages(changedFile?: string): string[] {
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

    private serializeSummary(summary: JavaScriptBuildSummary, changedFile?: string, skipped = false) {
        const data: Record<string, unknown> = {
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

    private emitJavaScriptFailure(error: unknown, changedFile?: string): void {
        let message = 'JavaScript rebuild failed.';
        let severity: DiagnosticSeverity = 'error';
        const data: Record<string, unknown> = changedFile ? { changedFile } : {};

        if (error instanceof JavaScriptBuildError) {
            message = `JavaScript rebuild failed for page '${error.pageName}'.`;
            if (error.details.length > 0) {
                data.errors = error.details;
            }
        } else if (error instanceof Error) {
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

    private resolveChangedFile(changedFile?: string): string | undefined {
        if (!changedFile) {
            return undefined;
        }

        if (path.isAbsolute(changedFile)) {
            return changedFile;
        }

        return path.resolve(this.workspaceRoot, changedFile);
    }

    private requireConfig(): FrontendConfig {
        if (!this.config) {
            throw new Error('Watch coordinator not initialized.');
        }
        return this.config;
    }

    private logUnexpectedError(stage: string, error: unknown): void {
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
    public readonly pageName: string;
    public readonly details: readonly SerializedMessage[];

    public constructor(pageName: string, cause: unknown) {
        const message = cause instanceof Error ? cause.message : String(cause);
        super(message);
        this.pageName = pageName;
        this.details = isBuildFailure(cause) ? serializeMessages(cause.errors ?? []) : [];
    }
}

async function resolveEntryPoint(pageDirectory: string): Promise<string | null> {
    const candidates = [`${FILES.index}${EXTENSIONS.ts}`, `${FILES.index}.tsx`, `${FILES.index}${EXTENSIONS.js}`, `${FILES.index}.jsx`];

    for (const candidate of candidates) {
        const file = path.join(pageDirectory, candidate);
        if (await pathExists(file)) {
            return file;
        }
    }

    return null;
}

async function copyRefreshScript(config: FrontendConfig): Promise<void> {
    const source = path.join(config.paths.src.app, FILES.refreshJs);
    if (!(await pathExists(source))) {
        return;
    }

    const destination = path.join(config.paths.build.frontend, FILES.refreshJs);
    await ensureDir(path.dirname(destination));
    await copy(source, destination);
}

function isBuildFailure(error: unknown): error is BuildFailure {
    if (typeof error !== 'object' || error === null) {
        return false;
    }

    return Array.isArray((error as BuildFailure).errors);
}

function serializeMessages(messages: readonly Message[]): SerializedMessage[] {
    return messages.map((message) => ({
        text: message.text,
        location: message.location
            ? {
                  file: message.location.file,
                  line: message.location.line,
                  column: message.location.column
              }
            : undefined
    }));
}

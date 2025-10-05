import type { BuildFailure, Message } from 'esbuild';
import { emitDiagnostic } from '../core/diagnostics.js';
import type { DiagnosticSeverity } from '../core/diagnostics.js';
import type { Builder, BuilderContext } from '../builders/types.js';
import { WatchReporter, serializeMessages, type SerializedMessage } from './watchReporter.js';
import type { HotAsset, HotUpdateDetails } from './hotUpdateTracker.js';

const BUILDER_DISPLAY_NAMES: Record<string, string> = {
    css: 'CSS',
    html: 'HTML',
    'static-assets': 'Static assets'
} as const;

export interface JavaScriptBuildSummary {
    readonly pagesBuilt: readonly string[];
    readonly warnings: readonly SerializedMessage[];
    readonly modules: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
}

export interface AdditionalBuildResult {
    readonly succeeded: boolean;
    readonly assets: readonly string[];
    readonly styles: readonly HotAsset[];
    readonly requiresReload: boolean;
    readonly fallbackReasons: readonly string[];
}

export async function runBuilderWithDiagnostics(
    builder: Builder,
    reporter: WatchReporter,
    context: BuilderContext,
    changedFile: string | undefined,
    relativeChange: string | undefined
): Promise<boolean> {
    const displayName = BUILDER_DISPLAY_NAMES[builder.name] ?? builder.name;
    const messageContext = relativeChange ? ` (${relativeChange})` : '';

    reporter.emitVerbose({
        code: `frontend.watch.${builder.name}.build.start`,
        kind: 'watch-daemon',
        stage: builder.name,
        severity: 'info',
        message: `Starting ${displayName} rebuild${messageContext}.`,
        data: changedFile ? { changedFile, builder: builder.name } : { builder: builder.name }
    });

    try {
        await builder.build(context);
        reporter.emitVerbose({
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

export function emitPipelineSuccess(
    summary: JavaScriptBuildSummary,
    assetsResult: AdditionalBuildResult,
    changedFile: string | undefined,
    relativeChange: string | undefined,
    hotUpdate: HotUpdateDetails
): void {
    const message = `Frontend rebuild pipeline completed${relativeChange ? ` (${relativeChange})` : ''}.`;

    const data: Record<string, unknown> = {
        pages: summary.pagesBuilt,
        assets: assetsResult.assets,
        hotUpdate: serializeHotUpdate(hotUpdate, relativeChange)
    };

    if (relativeChange) {
        data.changedFile = relativeChange;
    } else if (changedFile) {
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

export function serializeSummary(
    summary: JavaScriptBuildSummary,
    changedFile: string | undefined,
    skipped: boolean
): Record<string, unknown> {
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

    if (summary.modules.length > 0) {
        data.modules = summary.modules.map(asset => asset.url);
    }

    if (summary.requiresReload) {
        data.requiresReload = true;
    }

    if (summary.fallbackReasons.length > 0) {
        data.fallbackReasons = summary.fallbackReasons;
    }

    return data;
}

export function emitJavaScriptFailure(error: unknown, changedFile?: string): void {
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

export class JavaScriptBuildError extends Error {
    public readonly pageName: string;
    public readonly details: readonly SerializedMessage[];

    public constructor(pageName: string, cause: unknown) {
        const message = cause instanceof Error ? cause.message : String(cause);
        super(message);
        this.pageName = pageName;
        this.details = isBuildFailure(cause) ? serializeMessages(cause.errors ?? []) : [];
    }
}

function serializeHotUpdate(hotUpdate: HotUpdateDetails, relativeChange?: string): Record<string, unknown> {
    const data: Record<string, unknown> = {
        requiresReload: hotUpdate.requiresReload,
        modules: hotUpdate.modules.map(asset => serializeHotAsset(asset)),
        styles: hotUpdate.styles.map(asset => serializeHotAsset(asset))
    };

    if (relativeChange) {
        data.changedFile = relativeChange;
    } else if (hotUpdate.changedFile) {
        data.changedFile = hotUpdate.changedFile;
    }

    if (hotUpdate.fallbackReasons.length > 0) {
        data.fallbackReasons = hotUpdate.fallbackReasons;
    }

    if (hotUpdate.stats) {
        data.stats = {
            hotUpdates: hotUpdate.stats.hotUpdates,
            reloadFallbacks: hotUpdate.stats.reloadFallbacks
        };
    }

    return data;
}

function serializeHotAsset(asset: HotAsset): Record<string, string> {
    return {
        type: asset.type,
        path: asset.path,
        relativePath: asset.relativePath,
        url: asset.url
    };
}

function isBuildFailure(error: unknown): error is BuildFailure {
    if (typeof error !== 'object' || error === null) {
        return false;
    }

    const candidate = error as BuildFailure;
    return Array.isArray(candidate.errors) && candidate.errors.every(isEsbuildMessage);
}

function isEsbuildMessage(candidate: unknown): candidate is Message {
    if (typeof candidate !== 'object' || candidate === null) {
        return false;
    }

    return typeof (candidate as Message).text === 'string';
}

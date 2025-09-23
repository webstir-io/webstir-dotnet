import { emitDiagnostic } from '../core/diagnostics.js';
import { serializeMessages } from './watchReporter.js';
const BUILDER_DISPLAY_NAMES = {
    css: 'CSS',
    html: 'HTML',
    'static-assets': 'Static assets'
};
export async function runBuilderWithDiagnostics(builder, reporter, context, changedFile, relativeChange) {
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
export function emitPipelineSuccess(summary, assetsResult, changedFile, relativeChange, hotUpdate) {
    const message = `Frontend rebuild pipeline completed${relativeChange ? ` (${relativeChange})` : ''}.`;
    const data = {
        pages: summary.pagesBuilt,
        assets: assetsResult.assets,
        hotUpdate: serializeHotUpdate(hotUpdate, relativeChange)
    };
    if (relativeChange) {
        data.changedFile = relativeChange;
    }
    else if (changedFile) {
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
export function serializeSummary(summary, changedFile, skipped) {
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
export function emitJavaScriptFailure(error, changedFile) {
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
export class JavaScriptBuildError extends Error {
    pageName;
    details;
    constructor(pageName, cause) {
        const message = cause instanceof Error ? cause.message : String(cause);
        super(message);
        this.pageName = pageName;
        this.details = isBuildFailure(cause) ? serializeMessages(cause.errors ?? []) : [];
    }
}
function serializeHotUpdate(hotUpdate, relativeChange) {
    const data = {
        requiresReload: hotUpdate.requiresReload,
        modules: hotUpdate.modules.map(asset => serializeHotAsset(asset)),
        styles: hotUpdate.styles.map(asset => serializeHotAsset(asset))
    };
    if (relativeChange) {
        data.changedFile = relativeChange;
    }
    else if (hotUpdate.changedFile) {
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
function serializeHotAsset(asset) {
    return {
        type: asset.type,
        path: asset.path,
        relativePath: asset.relativePath,
        url: asset.url
    };
}
function isBuildFailure(error) {
    if (typeof error !== 'object' || error === null) {
        return false;
    }
    const candidate = error;
    return Array.isArray(candidate.errors) && candidate.errors.every(isEsbuildMessage);
}
function isEsbuildMessage(candidate) {
    if (typeof candidate !== 'object' || candidate === null) {
        return false;
    }
    return typeof candidate.text === 'string';
}

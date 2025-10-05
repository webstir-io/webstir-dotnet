import { emitDiagnostic } from '../core/diagnostics.js';
export class WatchReporter {
    verbose;
    constructor(options) {
        this.verbose = options.verbose;
    }
    emit(event) {
        emitDiagnostic(event);
    }
    emitVerbose(event) {
        if (!this.verbose) {
            return;
        }
        emitDiagnostic(event);
    }
    emitJavaScriptStats(pageName, result, durationMs) {
        if (!this.verbose) {
            return;
        }
        const stats = extractMetafileStats(result);
        const data = {
            page: pageName,
            durationMs: Number(durationMs.toFixed(1))
        };
        if (stats) {
            data.inputs = stats.inputs;
            data.outputs = stats.outputs;
            data.bytes = stats.bytes;
        }
        emitDiagnostic({
            code: 'frontend.watch.javascript.build.stats',
            kind: 'watch-daemon',
            stage: 'javascript',
            severity: 'info',
            message: `JavaScript rebuild stats for '${pageName}' (${durationMs.toFixed(1)}ms).`,
            data
        });
    }
}
export function serializeMessages(messages) {
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
function extractMetafileStats(result) {
    if (!result.metafile) {
        return null;
    }
    const inputs = Object.keys(result.metafile.inputs ?? {}).length;
    const outputsEntries = Object.entries(result.metafile.outputs ?? {});
    const bytes = outputsEntries.reduce((sum, [, output]) => sum + (output.bytes ?? 0), 0);
    return {
        inputs,
        outputs: outputsEntries.length,
        bytes
    };
}

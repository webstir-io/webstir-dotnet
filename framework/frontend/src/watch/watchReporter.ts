import type { BuildResult, Message } from 'esbuild';
import { emitDiagnostic } from '../core/diagnostics.js';
import type { DiagnosticEvent } from '../core/diagnostics.js';

export interface WatchReporterOptions {
    readonly verbose: boolean;
}

export interface SerializedMessage {
    readonly text: string;
    readonly location?: {
        readonly file?: string;
        readonly line?: number;
        readonly column?: number;
    };
}

export class WatchReporter {
    private readonly verbose: boolean;

    public constructor(options: WatchReporterOptions) {
        this.verbose = options.verbose;
    }

    public emit(event: DiagnosticEvent): void {
        emitDiagnostic(event);
    }

    public emitVerbose(event: DiagnosticEvent): void {
        if (!this.verbose) {
            return;
        }

        emitDiagnostic(event);
    }

    public emitJavaScriptStats(pageName: string, result: BuildResult, durationMs: number): void {
        if (!this.verbose) {
            return;
        }

        const stats = extractMetafileStats(result);
        const data: Record<string, unknown> = {
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

export function serializeMessages(messages: readonly Message[]): SerializedMessage[] {
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

function extractMetafileStats(result: BuildResult): MetafileStats | null {
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

interface MetafileStats {
    readonly inputs: number;
    readonly outputs: number;
    readonly bytes: number;
}

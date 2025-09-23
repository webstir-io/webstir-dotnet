import type { BuildResult, Message } from 'esbuild';
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
export declare class WatchReporter {
    private readonly verbose;
    constructor(options: WatchReporterOptions);
    emit(event: DiagnosticEvent): void;
    emitVerbose(event: DiagnosticEvent): void;
    emitJavaScriptStats(pageName: string, result: BuildResult, durationMs: number): void;
}
export declare function serializeMessages(messages: readonly Message[]): SerializedMessage[];

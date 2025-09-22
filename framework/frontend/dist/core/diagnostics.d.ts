export type DiagnosticSeverity = 'info' | 'warning' | 'error';
export interface DiagnosticEvent {
    readonly code: string;
    readonly kind: string;
    readonly stage: string;
    readonly severity: DiagnosticSeverity;
    readonly message: string;
    readonly data?: Record<string, unknown>;
    readonly suggestion?: string;
}
export interface DiagnosticPayload extends DiagnosticEvent {
    readonly type: 'diagnostic';
}
export declare const STRUCTURED_DIAGNOSTIC_PREFIX = "WEBSTIR_DIAGNOSTIC ";
export declare function emitDiagnostic(event: DiagnosticEvent): void;

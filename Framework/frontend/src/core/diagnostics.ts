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

export const STRUCTURED_DIAGNOSTIC_PREFIX = 'WEBSTIR_DIAGNOSTIC ';

export function emitDiagnostic(event: DiagnosticEvent): void {
    const payload: DiagnosticPayload = {
        type: 'diagnostic',
        ...event
    };

    const logMessage = `[webstir-frontend][${event.code}] ${event.message}`;
    switch (event.severity) {
        case 'error':
            console.error(logMessage);
            break;
        case 'warning':
            console.warn(logMessage);
            break;
        default:
            console.info(logMessage);
            break;
    }

    const serialized = JSON.stringify(payload);
    console.log(`${STRUCTURED_DIAGNOSTIC_PREFIX}${serialized}`);
}

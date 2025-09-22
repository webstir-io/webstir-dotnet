export const STRUCTURED_DIAGNOSTIC_PREFIX = 'WEBSTIR_DIAGNOSTIC ';
export function emitDiagnostic(event) {
    const payload = {
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

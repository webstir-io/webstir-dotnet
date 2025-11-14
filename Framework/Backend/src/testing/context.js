const GLOBAL_KEY = '__webstirBackendTestContext__';
export function setBackendTestContext(context) {
    const target = globalThis;
    if (context) {
        target[GLOBAL_KEY] = context;
    }
    else {
        delete target[GLOBAL_KEY];
    }
}
export function getBackendTestContext() {
    const target = globalThis;
    return (target[GLOBAL_KEY] ?? null);
}

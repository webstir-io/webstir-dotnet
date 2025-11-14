import type { BackendTestContext } from './types.js';

const GLOBAL_KEY = '__webstirBackendTestContext__';

export function setBackendTestContext(context: BackendTestContext | null): void {
    const target = globalThis as Record<string, unknown>;
    if (context) {
        target[GLOBAL_KEY] = context;
    } else {
        delete target[GLOBAL_KEY];
    }
}

export function getBackendTestContext(): BackendTestContext | null {
    const target = globalThis as Record<string, unknown>;
    return (target[GLOBAL_KEY] ?? null) as BackendTestContext | null;
}

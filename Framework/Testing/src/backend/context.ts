import type { BackendTestContext } from './types.js';

const GLOBAL_KEY = Symbol.for('webstir.backendTestContext');

export function setBackendTestContext(context: BackendTestContext | null): void {
  const store = globalThis as Record<string | symbol, unknown>;
  if (context) {
    store[GLOBAL_KEY] = context;
  } else if (GLOBAL_KEY in store) {
    delete store[GLOBAL_KEY];
  }
}

export function getBackendTestContext(): BackendTestContext | null {
  const store = globalThis as Record<string | symbol, unknown>;
  return (store[GLOBAL_KEY] as BackendTestContext | undefined) ?? null;
}

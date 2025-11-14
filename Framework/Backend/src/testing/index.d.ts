import { getBackendTestContext, setBackendTestContext } from './context.js';
import type { BackendTestCallback, BackendTestHarness, BackendTestHarnessOptions } from './types.js';
export type { BackendTestCallback, BackendTestContext, BackendTestHarness, BackendTestHarnessOptions } from './types.js';
export { getBackendTestContext, setBackendTestContext };
export declare function createBackendTestHarness(options?: BackendTestHarnessOptions): Promise<BackendTestHarness>;
export declare function backendTest(name: string, callback: BackendTestCallback): void;

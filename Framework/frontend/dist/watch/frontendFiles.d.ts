import type { FrontendConfig } from '../types.js';
export declare function resolveEntryPoint(pageDirectory: string): Promise<string | null>;
export declare function copyRefreshScript(config: FrontendConfig): Promise<void>;

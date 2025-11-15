import type { ModuleDiagnostic, ModuleManifest } from '@webstir-io/module-contract';
import type { BackendBuildMode } from '../workspace.js';
export interface CacheReporter {
    readonly diffOutputs: (outputs: Record<string, number> | undefined, mode: BackendBuildMode) => Promise<void>;
    readonly diffManifest: (manifest: ModuleManifest) => Promise<void>;
}
export declare function createCacheReporter(options: {
    readonly workspaceRoot: string;
    readonly buildRoot: string;
    readonly env: Record<string, string | undefined>;
    readonly diagnostics: ModuleDiagnostic[];
}): CacheReporter;

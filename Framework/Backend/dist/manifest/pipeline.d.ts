import type { ModuleDiagnostic, ModuleManifest } from '@webstir-io/module-contract';
export interface LoadManifestOptions {
    readonly workspaceRoot: string;
    readonly buildRoot: string;
    readonly entryPoints: readonly string[];
    readonly diagnostics: ModuleDiagnostic[];
}
export declare function loadBackendModuleManifest(options: LoadManifestOptions): Promise<ModuleManifest>;
export declare function summarizeBuiltManifest(buildRoot: string): Promise<{
    routes: number;
    views: number;
    capabilities?: readonly string[];
} | undefined>;

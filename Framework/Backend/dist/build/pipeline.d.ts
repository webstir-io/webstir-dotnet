import type { ModuleDiagnostic } from '@webstir-io/module-contract';
import type { BackendBuildMode } from '../workspace.js';
export interface BackendBuildPipelineOptions {
    readonly sourceRoot: string;
    readonly buildRoot: string;
    readonly tsconfigPath: string;
    readonly mode: BackendBuildMode;
    readonly env: Record<string, string | undefined>;
    readonly incremental: boolean;
    readonly diagnostics: ModuleDiagnostic[];
}
export interface BackendBuildPipelineResult {
    readonly entryPoints: readonly string[];
    readonly outputs?: Record<string, number>;
    readonly includePublishSourcemaps: boolean;
}
export declare function runBackendBuildPipeline(options: BackendBuildPipelineOptions): Promise<BackendBuildPipelineResult>;
export declare function shouldTypeCheck(mode: BackendBuildMode, env: Record<string, string | undefined>): boolean;
interface SupportFileBuildOptions {
    readonly sourceFile: string;
    readonly sourceRoot: string;
    readonly buildRoot: string;
    readonly tsconfigPath: string;
    readonly mode: BackendBuildMode;
    readonly env: Record<string, string | undefined>;
    readonly diagnostics: ModuleDiagnostic[];
}
export declare function buildSupportFile(options: SupportFileBuildOptions): Promise<void>;
export declare function collectOutputSizes(metafile: unknown, buildRoot: string): Record<string, number>;
export declare function formatEsbuildMessage(msg: any): string;
export {};

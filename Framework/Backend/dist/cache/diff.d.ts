import type { ModuleManifest, ModuleDiagnostic } from '@webstir-io/module-contract';
import type { BackendBuildMode } from '../workspace.js';
export declare function persistAndDiffOutputs(workspaceRoot: string, _buildRoot: string, outputs: Record<string, number> | undefined, env: Record<string, string | undefined>, diagnostics: ModuleDiagnostic[], mode: BackendBuildMode): Promise<void>;
export declare function persistAndDiffManifest(workspaceRoot: string, manifest: ModuleManifest, env: Record<string, string | undefined>, diagnostics: ModuleDiagnostic[]): Promise<void>;

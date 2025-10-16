export type ModuleKind = 'frontend' | 'backend';

export interface ModuleCompatibility {
  readonly minCliVersion: string;
  readonly maxCliVersion?: string;
  readonly nodeRange: string;
  readonly notes?: string;
}

export interface ModuleProviderMetadata {
  readonly id: string;
  readonly kind: ModuleKind;
  readonly version: string;
  readonly compatibility: ModuleCompatibility;
}

export interface ResolveWorkspaceOptions {
  readonly workspaceRoot: string;
  readonly config: Record<string, unknown>;
}

export interface ResolvedModuleWorkspace {
  readonly sourceRoot: string;
  readonly buildRoot: string;
  readonly testsRoot?: string;
}

export interface ModuleBuildOptions {
  readonly workspaceRoot: string;
  readonly env: Record<string, string | undefined>;
  readonly incremental?: boolean;
}

export interface ModuleDiagnostic {
  readonly severity: 'info' | 'warn' | 'error';
  readonly message: string;
  readonly file?: string;
}

export interface ModuleBuildManifest {
  readonly entryPoints: readonly string[];
  readonly staticAssets: readonly string[];
  readonly diagnostics: readonly ModuleDiagnostic[];
}

export interface ModuleArtifact {
  readonly path: string;
  readonly type: 'asset' | 'bundle' | 'metadata';
}

export interface ModuleBuildResult {
  readonly artifacts: readonly ModuleArtifact[];
  readonly manifest: ModuleBuildManifest;
}

export interface ModuleAsset {
  readonly sourcePath: string;
  readonly targetPath: string;
}

export interface ModuleProvider {
  readonly metadata: ModuleProviderMetadata;
  resolveWorkspace(options: ResolveWorkspaceOptions): Promise<ResolvedModuleWorkspace> | ResolvedModuleWorkspace;
  build(options: ModuleBuildOptions): Promise<ModuleBuildResult> | ModuleBuildResult;
  getScaffoldAssets?(): Promise<readonly ModuleAsset[]> | readonly ModuleAsset[];
}

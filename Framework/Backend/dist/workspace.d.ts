import type { ResolvedModuleWorkspace } from '@webstir-io/module-contract';
export type BackendBuildMode = 'build' | 'publish' | 'test';
export declare function resolveWorkspacePaths(workspaceRoot: string): ResolvedModuleWorkspace;
export declare function normalizeMode(rawMode: unknown): BackendBuildMode;

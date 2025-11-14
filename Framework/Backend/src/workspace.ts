import path from 'node:path';

import type { ResolvedModuleWorkspace } from '@webstir-io/module-contract';

export type BackendBuildMode = 'build' | 'publish' | 'test';

export function resolveWorkspacePaths(workspaceRoot: string): ResolvedModuleWorkspace {
  return {
    sourceRoot: path.join(workspaceRoot, 'src', 'backend'),
    buildRoot: path.join(workspaceRoot, 'build', 'backend'),
    testsRoot: path.join(workspaceRoot, 'src', 'backend', 'tests')
  };
}

export function normalizeMode(rawMode: unknown): BackendBuildMode {
  if (typeof rawMode !== 'string') {
    return 'build';
  }

  const normalized = rawMode.toLowerCase();
  return normalized === 'publish' || normalized === 'test' ? normalized : 'build';
}

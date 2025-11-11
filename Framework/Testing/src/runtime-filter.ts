import type { TestManifest } from './types.js';

export type RuntimeFilter = 'frontend' | 'backend' | null;

export function normalizeRuntimeFilter(value: string | undefined | null): RuntimeFilter {
  const normalized = (value ?? '').trim().toLowerCase();
  if (!normalized || normalized === 'all') {
    return null;
  }

  if (normalized === 'frontend' || normalized === 'backend') {
    return normalized;
  }

  return null;
}

export function applyRuntimeFilter(manifest: TestManifest, runtime: RuntimeFilter): TestManifest {
  if (!runtime) {
    return manifest;
  }

  return {
    ...manifest,
    modules: manifest.modules.filter((module) => module.runtime === runtime),
  };
}

export function describeRuntimeFilter(runtime: RuntimeFilter, before: number, after: number): string | null {
  if (!runtime) {
    return null;
  }

  const skipped = Math.max(before - after, 0);
  const noun = after === 1 ? 'test' : 'tests';
  return `Runtime filter '${runtime}' matched ${after} ${noun} (${skipped} skipped).`;
}

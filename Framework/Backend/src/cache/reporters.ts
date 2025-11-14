import type { ModuleDiagnostic, ModuleManifest } from '@webstir-io/module-contract';

import type { BackendBuildMode } from '../workspace.js';
import { persistAndDiffManifest, persistAndDiffOutputs } from './diff.js';

export interface CacheReporter {
    readonly diffOutputs: (
        outputs: Record<string, number> | undefined,
        mode: BackendBuildMode
    ) => Promise<void>;
    readonly diffManifest: (manifest: ModuleManifest) => Promise<void>;
}

export function createCacheReporter(options: {
    readonly workspaceRoot: string;
    readonly buildRoot: string;
    readonly env: Record<string, string | undefined>;
    readonly diagnostics: ModuleDiagnostic[];
}): CacheReporter {
    const { workspaceRoot, buildRoot, env, diagnostics } = options;
    const diagnosticsTarget = shouldLogCacheDiffs(env) ? diagnostics : [];

    return {
        async diffOutputs(outputs, mode) {
            await persistAndDiffOutputs(workspaceRoot, buildRoot, outputs, env, diagnosticsTarget, mode);
        },
        async diffManifest(manifest) {
            await persistAndDiffManifest(workspaceRoot, manifest, env, diagnosticsTarget);
        }
    };
}

function shouldLogCacheDiffs(env: Record<string, string | undefined>): boolean {
    const raw = env?.WEBSTIR_BACKEND_CACHE_LOG;
    if (!raw) return true;
    const normalized = raw.trim().toLowerCase();
    if (['off', '0', 'false', 'quiet', 'silent', 'skip'].includes(normalized)) {
        return false;
    }
    return true;
}

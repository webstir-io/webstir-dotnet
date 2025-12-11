import { persistAndDiffManifest, persistAndDiffOutputs } from './diff.js';
export function createCacheReporter(options) {
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
function shouldLogCacheDiffs(env) {
    const raw = env?.WEBSTIR_BACKEND_CACHE_LOG;
    if (!raw)
        return true;
    const normalized = raw.trim().toLowerCase();
    if (['off', '0', 'false', 'quiet', 'silent', 'skip'].includes(normalized)) {
        return false;
    }
    return true;
}

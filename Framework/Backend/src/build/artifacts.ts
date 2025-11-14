import path from 'node:path';
import { existsSync } from 'node:fs';

import { glob } from 'glob';
import type { ModuleArtifact, ModuleDiagnostic, ModuleManifest } from '@webstir-io/module-contract';

import { pushEntryBucketSummary } from '../diagnostics/summary.js';

export async function collectArtifacts(buildRoot: string, includeSourceMaps: boolean): Promise<ModuleArtifact[]> {
    const patterns = ['**/*.js'];
    if (includeSourceMaps) {
        patterns.push('**/*.js.map');
    }
    const matches = new Set<string>();
    for (const pattern of patterns) {
        const files = await glob(pattern, {
            cwd: buildRoot,
            nodir: true,
            dot: false
        });
        for (const relativePath of files) {
            matches.add(relativePath);
        }
    }

    return Array.from(matches).map<ModuleArtifact>((relativePath) => ({
        path: path.join(buildRoot, relativePath),
        type: relativePath.endsWith('.map') ? 'asset' : 'bundle'
    }));
}

export function createBuildManifest(
    buildRoot: string,
    artifacts: readonly ModuleArtifact[],
    diagnostics: ModuleDiagnostic[],
    moduleManifest: ModuleManifest
) {
    const entryPoints: string[] = [];

    for (const artifact of artifacts) {
        const relative = path.relative(buildRoot, artifact.path);
        if (relative.endsWith('index.js')) {
            entryPoints.push(relative);
        }
    }

    if (entryPoints.length === 0) {
        const defaultEntry = path.join(buildRoot, 'index.js');
        if (existsSync(defaultEntry)) {
            entryPoints.push(path.relative(buildRoot, defaultEntry));
        } else {
            diagnostics.push({
                severity: 'warn',
                message: 'No backend entry point found (expected index.js).'
            });
        }
    }

    pushEntryBucketSummary(diagnostics, entryPoints);

    return {
        entryPoints,
        staticAssets: [],
        diagnostics,
        module: moduleManifest
    };
}

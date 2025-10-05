import { writeConfigManifest } from './manifest.js';
import { buildConfig } from './workspace.js';
import { ensureWebstirDirectory, resolveManifestPath } from './paths.js';
import type { FrontendConfig } from '../types.js';

export async function prepareWorkspaceConfig(workspaceRoot: string): Promise<FrontendConfig> {
    const config = buildConfig(workspaceRoot);
    await ensureWebstirDirectory(workspaceRoot);
    await writeConfigManifest({
        outputPath: resolveManifestPath(workspaceRoot),
        data: config
    });
    return config;
}

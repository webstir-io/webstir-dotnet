import { writeConfigManifest } from './manifest.js';
import { buildConfig } from './workspace.js';
import { ensureWebstirDirectory, resolveManifestPath } from './paths.js';
export async function prepareWorkspaceConfig(workspaceRoot) {
    const config = buildConfig(workspaceRoot);
    await ensureWebstirDirectory(workspaceRoot);
    await writeConfigManifest({
        outputPath: resolveManifestPath(workspaceRoot),
        data: config
    });
    return config;
}

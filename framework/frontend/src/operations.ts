import { writeConfigManifest } from './config/manifest.js';
import type { FrontendCommandOptions } from './types.js';
import { buildConfig } from './utils/workspace.js';
import { ensureToolsDirectory, resolveManifestPath } from './utils/manifest.js';
import { runPipeline } from './pipeline.js';

export async function runBuild(options: FrontendCommandOptions): Promise<void> {
    const config = buildConfig(options.workspaceRoot);
    await ensureToolsDirectory(options.workspaceRoot);
    await writeConfigManifest({
        outputPath: resolveManifestPath(options.workspaceRoot),
        data: config
    });

    console.info('[webstir-frontend] Running build pipeline...');
    await runPipeline(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Build pipeline completed.');
}

export async function runPublish(options: FrontendCommandOptions): Promise<void> {
    const config = buildConfig(options.workspaceRoot);
    await ensureToolsDirectory(options.workspaceRoot);
    await writeConfigManifest({
        outputPath: resolveManifestPath(options.workspaceRoot),
        data: config
    });

    console.info('[webstir-frontend] Running publish pipeline...');
    await runPipeline(config, 'publish');
    console.info('[webstir-frontend] Publish pipeline completed.');
}

export async function runRebuild(options: FrontendCommandOptions): Promise<void> {
    const config = buildConfig(options.workspaceRoot);
    await ensureToolsDirectory(options.workspaceRoot);
    await writeConfigManifest({
        outputPath: resolveManifestPath(options.workspaceRoot),
        data: config
    });

    console.info('[webstir-frontend] Running rebuild pipeline...');
    await runPipeline(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Rebuild pipeline completed.');
}

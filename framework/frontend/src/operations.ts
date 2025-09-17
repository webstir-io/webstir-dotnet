import { writeConfigManifest } from './config/manifest.js';
import type { AddPageCommandOptions, FrontendCommandOptions } from './types.js';
import { buildConfig } from './config/workspace.js';
import { ensureToolsDirectory, resolveManifestPath } from './config/paths.js';
import { runPipeline } from './pipeline.js';
import { createPageScaffold } from './html/pageScaffold.js';

async function prepareConfig(workspaceRoot: string) {
    const config = buildConfig(workspaceRoot);
    await ensureToolsDirectory(workspaceRoot);
    await writeConfigManifest({
        outputPath: resolveManifestPath(workspaceRoot),
        data: config
    });
    return config;
}

export async function runBuild(options: FrontendCommandOptions): Promise<void> {
    const config = await prepareConfig(options.workspaceRoot);

    console.info('[webstir-frontend] Running build pipeline...');
    await runPipeline(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Build pipeline completed.');
}

export async function runPublish(options: FrontendCommandOptions): Promise<void> {
    const config = await prepareConfig(options.workspaceRoot);

    console.info('[webstir-frontend] Running publish pipeline...');
    await runPipeline(config, 'publish');
    console.info('[webstir-frontend] Publish pipeline completed.');
}

export async function runRebuild(options: FrontendCommandOptions): Promise<void> {
    const config = await prepareConfig(options.workspaceRoot);

    console.info('[webstir-frontend] Running rebuild pipeline...');
    await runPipeline(config, 'build', { changedFile: options.changedFile });
    console.info('[webstir-frontend] Rebuild pipeline completed.');
}

export async function runAddPage(options: AddPageCommandOptions): Promise<void> {
    const config = await prepareConfig(options.workspaceRoot);
    console.info('[webstir-frontend] Creating page scaffold...');
    await createPageScaffold({
        workspaceRoot: options.workspaceRoot,
        pageName: options.pageName,
        paths: {
            pages: config.paths.src.pages,
            app: config.paths.src.app
        }
    });
    console.info('[webstir-frontend] Page scaffold created.');
}

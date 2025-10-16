import path from 'node:path';
import fs from 'node:fs';
import { glob } from 'glob';
import packageJson from '../package.json' with { type: 'json' };
import { runPipeline } from './pipeline.js';
import { prepareWorkspaceConfig } from './config/setup.js';
const pkg = packageJson;
function resolveWorkspacePaths(workspaceRoot) {
    return {
        sourceRoot: path.join(workspaceRoot, 'src', 'frontend'),
        buildRoot: path.join(workspaceRoot, 'build', 'frontend'),
        testsRoot: path.join(workspaceRoot, 'src', 'frontend', 'tests')
    };
}
async function buildModule(options) {
    const config = await prepareWorkspaceConfig(options.workspaceRoot);
    const mode = normalizeMode(options.env?.WEBSTIR_MODULE_MODE);
    await runPipeline(config, mode, { changedFile: undefined });
    const artifacts = await collectArtifacts(config);
    const manifest = createManifest(config, artifacts);
    return {
        artifacts,
        manifest
    };
}
function normalizeMode(rawMode) {
    if (typeof rawMode !== 'string') {
        return 'build';
    }
    return rawMode.toLowerCase() === 'publish' ? 'publish' : 'build';
}
async function getScaffoldAssets() {
    return [];
}
async function collectArtifacts(config) {
    const buildRoot = config.paths.build.frontend;
    const matches = await glob('**/*', {
        cwd: buildRoot,
        nodir: true,
        dot: false
    });
    return matches.map((relative) => {
        const absolutePath = path.join(buildRoot, relative);
        const ext = path.extname(relative).toLowerCase();
        const artifactType = ext === '.js' || ext === '.mjs' ? 'bundle' : 'asset';
        return {
            path: absolutePath,
            type: artifactType
        };
    });
}
function createManifest(config, assets) {
    const entryPoints = [];
    const staticAssets = [];
    const diagnostics = [];
    for (const asset of assets) {
        const relativePath = path.relative(config.paths.build.frontend, asset.path);
        const ext = path.extname(relativePath).toLowerCase();
        if (ext === '.js' || ext === '.mjs') {
            entryPoints.push(relativePath);
        }
        else if (ext) {
            staticAssets.push(relativePath);
        }
    }
    if (entryPoints.length === 0) {
        const fallback = path.join(config.paths.build.app, 'index.js');
        if (fs.existsSync(fallback)) {
            entryPoints.push(path.relative(config.paths.build.frontend, fallback));
        }
        else {
            diagnostics.push({
                severity: 'warn',
                message: 'No JavaScript entry points found under build/frontend.'
            });
        }
    }
    return {
        entryPoints,
        staticAssets,
        diagnostics
    };
}
export const frontendProvider = {
    metadata: {
        id: pkg.name ?? '@webstir-io/webstir-frontend',
        kind: 'frontend',
        version: pkg.version ?? '0.0.0',
        compatibility: {
            minCliVersion: '0.1.0',
            nodeRange: pkg.engines?.node ?? '>=20.18.1'
        }
    },
    resolveWorkspace(options) {
        return resolveWorkspacePaths(options.workspaceRoot);
    },
    async build(options) {
        return await buildModule(options);
    },
    async getScaffoldAssets() {
        return await getScaffoldAssets();
    }
};

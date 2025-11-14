import path from 'node:path';
import { existsSync } from 'node:fs';

import type {
    ModuleBuildOptions,
    ModuleBuildResult,
    ModuleDiagnostic,
    ModuleManifest,
    ModuleProvider
} from '@webstir-io/module-contract';

import { collectArtifacts, createBuildManifest } from './build/artifacts.js';
import { buildSupportFile, runBackendBuildPipeline } from './build/pipeline.js';
import { loadBackendModuleManifest } from './manifest/pipeline.js';
import { createCacheReporter } from './cache/reporters.js';
import { pushEntryBucketSummary, normalizeLogLevel, filterDiagnostics } from './diagnostics/summary.js';
import { getBackendScaffoldAssets } from './scaffold/assets.js';
import { normalizeMode, resolveWorkspacePaths } from './workspace.js';

import packageJson from '../package.json' with { type: 'json' };

interface PackageJson {
    readonly name: string;
    readonly version: string;
    readonly engines?: {
        readonly node?: string;
    };
}

const pkg = packageJson as PackageJson;

export const backendProvider: ModuleProvider = {
    metadata: {
        id: pkg.name ?? '@webstir-io/webstir-backend',
        kind: 'backend',
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
        const paths = resolveWorkspacePaths(options.workspaceRoot);
        const tsconfigPath = path.join(paths.sourceRoot, 'tsconfig.json');

        const diagnostics: ModuleDiagnostic[] = [];

        const incremental = options.incremental === true;
        const env = options.env ?? {};
        const mode = normalizeMode(env.WEBSTIR_MODULE_MODE);
        console.info(`[webstir-backend] ${mode}:start`);

        const { entryPoints, outputs, includePublishSourcemaps } = await runBackendBuildPipeline({
            sourceRoot: paths.sourceRoot,
            buildRoot: paths.buildRoot,
            tsconfigPath,
            mode,
            env,
            incremental,
            diagnostics
        });

        const artifacts = await collectArtifacts(paths.buildRoot, includePublishSourcemaps);
        const envSource = path.join(paths.sourceRoot, 'env.ts');
        if (existsSync(envSource)) {
            try {
                await buildSupportFile({
                    sourceFile: envSource,
                    sourceRoot: paths.sourceRoot,
                    buildRoot: paths.buildRoot,
                    tsconfigPath,
                    mode,
                    env,
                    diagnostics
                });
            } catch {
                // env compilation errors are already captured in diagnostics
            }
        }
        const moduleManifest = await loadBackendModuleManifest({
            workspaceRoot: options.workspaceRoot,
            buildRoot: paths.buildRoot,
            entryPoints,
            diagnostics
        });
        const manifest = createBuildManifest(paths.buildRoot, artifacts, diagnostics, moduleManifest);

        console.info(`[webstir-backend] ${mode}:complete (entries=${manifest.entryPoints.length})`);
        diagnostics.push({ severity: 'info', message: `[webstir-backend] ${mode}:built entries=${manifest.entryPoints.length}` });
        const cacheReporter = createCacheReporter({
            workspaceRoot: options.workspaceRoot,
            buildRoot: paths.buildRoot,
            env,
            diagnostics
        });
        try {
            await cacheReporter.diffOutputs(outputs, mode);
        } catch {
            // ignore cache errors
        }
        try {
            await cacheReporter.diffManifest(moduleManifest);
        } catch {
            // ignore cache errors
        }
        // Optionally filter diagnostics by severity for orchestrator consumption
        const minLevel = normalizeLogLevel(env.WEBSTIR_BACKEND_LOG_LEVEL);
        const filteredDiagnostics = filterDiagnostics(manifest.diagnostics, minLevel);

        return {
            artifacts,
            manifest: {
                ...manifest,
                diagnostics: filteredDiagnostics
            }
        };
    },
    async getScaffoldAssets() {
        return await getBackendScaffoldAssets();
    }
};

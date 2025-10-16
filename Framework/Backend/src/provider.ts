import path from 'node:path';
import { existsSync } from 'node:fs';
import { spawn } from 'node:child_process';

import { glob } from 'glob';
import type {
    ModuleArtifact,
    ModuleBuildOptions,
    ModuleBuildResult,
    ModuleDiagnostic,
    ModuleProvider,
    ResolvedModuleWorkspace
} from '@webstir-io/module-contract';

import packageJson from '../package.json' with { type: 'json' };

interface PackageJson {
    readonly name: string;
    readonly version: string;
    readonly engines?: {
        readonly node?: string;
    };
}

const pkg = packageJson as PackageJson;

function resolveWorkspacePaths(workspaceRoot: string): ResolvedModuleWorkspace {
    return {
        sourceRoot: path.join(workspaceRoot, 'src', 'backend'),
        buildRoot: path.join(workspaceRoot, 'build', 'backend'),
        testsRoot: path.join(workspaceRoot, 'src', 'backend', 'tests')
    };
}

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

        await runTsc(tsconfigPath, options.env, diagnostics);

        const artifacts = await collectArtifacts(paths.buildRoot);
        const manifest = createManifest(paths.buildRoot, artifacts, diagnostics);

        return {
            artifacts,
            manifest
        };
    }
};

async function collectArtifacts(buildRoot: string): Promise<ModuleArtifact[]> {
    const matches = await glob('**/*.js', {
        cwd: buildRoot,
        nodir: true,
        dot: false
    });

    return matches.map<ModuleArtifact>((relativePath) => ({
        path: path.join(buildRoot, relativePath),
        type: 'bundle'
    }));
}

function createManifest(buildRoot: string, artifacts: readonly ModuleArtifact[], diagnostics: ModuleDiagnostic[]) {
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

    return {
        entryPoints,
        staticAssets: [],
        diagnostics
    };
}

async function runTsc(tsconfigPath: string, env: Record<string, string | undefined>, diagnostics: ModuleDiagnostic[]): Promise<void> {
    if (!existsSync(tsconfigPath)) {
        diagnostics.push({
            severity: 'warn',
            message: `TypeScript config not found at ${tsconfigPath}; skipping compile.`
        });
        return;
    }

    await new Promise<void>((resolve, reject) => {
        const child = spawn('tsc', ['-p', tsconfigPath], {
            stdio: 'pipe',
            env: {
                ...process.env,
                ...env
            }
        });

        let stdout = '';
        let stderr = '';

        child.stdout?.on('data', (chunk) => {
            stdout += chunk.toString();
        });

        child.stderr?.on('data', (chunk) => {
            stderr += chunk.toString();
        });

        child.on('error', reject);
        child.on('close', (code) => {
            if (code === 0) {
                resolve();
            } else {
                diagnostics.push({
                    severity: 'error',
                    message: `Backend TypeScript compilation failed (exit code ${code}).`,
                    file: tsconfigPath
                });
                if (stderr) {
                    diagnostics.push({
                        severity: 'error',
                        message: stderr.trim()
                    });
                }
                if (stdout) {
                    diagnostics.push({
                        severity: 'info',
                        message: stdout.trim()
                    });
                }
                reject(new Error('TypeScript compilation failed.'));
            }
        });
    });
}

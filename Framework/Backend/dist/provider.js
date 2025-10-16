import path from 'node:path';
import { existsSync } from 'node:fs';
import { spawn } from 'node:child_process';
import { performance } from 'node:perf_hooks';
import { glob } from 'glob';
import packageJson from '../package.json' with { type: 'json' };
const pkg = packageJson;
function resolveWorkspacePaths(workspaceRoot) {
    return {
        sourceRoot: path.join(workspaceRoot, 'src', 'backend'),
        buildRoot: path.join(workspaceRoot, 'build', 'backend'),
        testsRoot: path.join(workspaceRoot, 'src', 'backend', 'tests')
    };
}
export const backendProvider = {
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
        const diagnostics = [];
        const incremental = options.incremental === true;
        const mode = normalizeMode(options.env?.WEBSTIR_MODULE_MODE);
        await runTsc(tsconfigPath, options.env, diagnostics, incremental, mode);
        const artifacts = await collectArtifacts(paths.buildRoot);
        const manifest = createManifest(paths.buildRoot, artifacts, diagnostics);
        return {
            artifacts,
            manifest
        };
    }
};
function normalizeMode(rawMode) {
    if (typeof rawMode !== 'string') {
        return 'build';
    }
    const normalized = rawMode.toLowerCase();
    if (normalized === 'publish' || normalized === 'test') {
        return normalized;
    }
    return 'build';
}
async function collectArtifacts(buildRoot) {
    const matches = await glob('**/*.js', {
        cwd: buildRoot,
        nodir: true,
        dot: false
    });
    return matches.map((relativePath) => ({
        path: path.join(buildRoot, relativePath),
        type: 'bundle'
    }));
}
function createManifest(buildRoot, artifacts, diagnostics) {
    const entryPoints = [];
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
        }
        else {
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
async function runTsc(tsconfigPath, env, diagnostics, incremental, mode) {
    if (!existsSync(tsconfigPath)) {
        diagnostics.push({
            severity: 'warn',
            message: `TypeScript config not found at ${tsconfigPath}; skipping compile.`
        });
        return;
    }
    await new Promise((resolve, reject) => {
        const args = ['-p', tsconfigPath];
        if (incremental) {
            args.push('--incremental');
        }
        const child = spawn('tsc', args, {
            stdio: 'pipe',
            env: {
                ...process.env,
                ...env,
                NODE_ENV: env?.NODE_ENV ?? (mode === 'publish' ? 'production' : 'development')
            }
        });
        let stdout = '';
        let stderr = '';
        const start = performance.now();
        child.stdout?.on('data', (chunk) => {
            stdout += chunk.toString();
        });
        child.stderr?.on('data', (chunk) => {
            stderr += chunk.toString();
        });
        child.on('error', reject);
        child.on('close', (code) => {
            if (code === 0) {
                const end = performance.now();
                console.info(`[webstir-backend] ${mode}:tsc completed in ${(end - start).toFixed(1)}ms`);
                resolve();
            }
            else {
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

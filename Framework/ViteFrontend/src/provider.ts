import path from 'node:path';
import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';

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
  readonly engines?: { readonly node?: string };
}

const pkg = packageJson as PackageJson;

const viteProvider: ModuleProvider = {
  metadata: {
    id: pkg.name ?? '@webstir-io/webstir-frontend-vite',
    kind: 'frontend',
    version: pkg.version ?? '0.0.0',
    compatibility: {
      minCliVersion: '0.1.0',
      nodeRange: pkg.engines?.node ?? '>=20.18.1'
    }
  },
  resolveWorkspace(options) {
    const workspaceRoot = options.workspaceRoot;
    return {
      sourceRoot: path.join(workspaceRoot, 'src', 'frontend'),
      buildRoot: path.join(workspaceRoot, 'build', 'frontend'),
      testsRoot: path.join(workspaceRoot, 'src', 'frontend', 'tests')
    } satisfies ResolvedModuleWorkspace;
  },
  async build(options) {
    const mode = normalizeMode(options.env?.WEBSTIR_MODULE_MODE);
    const configPath = path.join(options.workspaceRoot, 'vite.config.ts');
    const diagnostics: ModuleDiagnostic[] = [];

    await runVite(mode, configPath, options.workspaceRoot, diagnostics, options.env);

    const buildRoot = path.join(options.workspaceRoot, 'build', 'frontend');
    const artifacts = await collectArtifacts(buildRoot);
    const manifest = buildManifest(buildRoot, artifacts, diagnostics);

    return { artifacts, manifest } satisfies ModuleBuildResult;
  }
};

export { viteProvider };
export default viteProvider;

function normalizeMode(mode: unknown): 'build' | 'publish' {
  if (typeof mode !== 'string') {
    return 'build';
  }
  return mode.toLowerCase() === 'publish' ? 'publish' : 'build';
}

async function runVite(mode: 'build' | 'publish', configPath: string, workspaceRoot: string, diagnostics: ModuleDiagnostic[], env?: Record<string, string | undefined>): Promise<void> {
  if (!existsSync(configPath)) {
    diagnostics.push({
      severity: 'warn',
      message: `Vite config not found at ${configPath}; skipping build.`
    });
    return;
  }

  const outDir = path.join(workspaceRoot, 'build', 'frontend');
  const args = ['build', '--config', configPath, '--mode', mode === 'publish' ? 'production' : 'development', '--outDir', outDir];
  const binName = process.platform === 'win32' ? 'vite.cmd' : 'vite';
  const command = path.join(workspaceRoot, 'node_modules', '.bin', binName);

  await spawnAsync(command, args, {
    cwd: workspaceRoot,
    env: {
      ...process.env,
      ...Object.fromEntries(Object.entries(env ?? {}))
    }
  }, diagnostics);
}

async function spawnAsync(command: string, args: string[], options: { cwd: string; env: NodeJS.ProcessEnv }, diagnostics: ModuleDiagnostic[]): Promise<void> {
  await new Promise<void>((resolve, reject) => {
    const child = spawn(command, args, {
      cwd: options.cwd,
      env: options.env,
      stdio: ['ignore', 'pipe', 'pipe']
    });

    child.stdout?.on('data', (chunk) => {
      const message = chunk.toString();
      emitLog({ type: 'info', message });
    });

    let stderr = '';
    child.stderr?.on('data', (chunk) => {
      const chunkValue = chunk.toString();
      stderr += chunkValue;
      emitLog({ type: 'warn', message: chunkValue });
    });

    child.on('error', reject);
    child.on('close', (code) => {
      if (code === 0) {
        resolve();
      } else {
        diagnostics.push({
          severity: 'error',
          message: `Vite exited with code ${code}.`
        });
        if (stderr.trim().length > 0) {
          diagnostics.push({
            severity: 'error',
            message: stderr.trim()
          });
        }
        reject(new Error(`Vite exited with code ${code}`));
      }
    });
  });
}

function emitLog(event: { type: string; message: string }): void {
  try {
    process.stderr.write(`WEBSTIR_MODULE_EVENT ${JSON.stringify(event)}\n`);
  } catch {
    // Ignore serialization issues.
  }
}

async function collectArtifacts(buildRoot: string): Promise<ModuleArtifact[]> {
  const matches = await glob('**/*', {
    cwd: buildRoot,
    nodir: true,
    dot: false
  });

  return matches.map<ModuleArtifact>((relative) => ({
    path: path.join(buildRoot, relative),
    type: classifyArtifact(relative)
  }));
}

function buildManifest(buildRoot: string, artifacts: readonly ModuleArtifact[], diagnostics: ModuleDiagnostic[]) {
  const entryPoints: string[] = [];
  const staticAssets: string[] = [];

  for (const artifact of artifacts) {
    const relative = path.relative(buildRoot, artifact.path);
    const ext = path.extname(relative).toLowerCase();
    if (ext === '.js' || ext === '.mjs') {
      entryPoints.push(relative);
    } else if (ext !== '') {
      staticAssets.push(relative);
    }
  }

  if (entryPoints.length === 0) {
    diagnostics.push({
      severity: 'warn',
      message: 'No Vite entry points discovered.'
    });
  }

  return {
    entryPoints,
    staticAssets,
    diagnostics
  };
}

function classifyArtifact(relative: string): 'bundle' | 'asset' {
  const ext = path.extname(relative).toLowerCase();
  return ext === '.js' || ext === '.mjs' ? 'bundle' : 'asset';
}

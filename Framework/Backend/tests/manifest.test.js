import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { backendProvider } from '../dist/index.js';

async function createTempWorkspace(prefix = 'webstir-backend-manifest-') {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), prefix));
  return dir;
}

async function ensureDir(dir) {
  await fs.mkdir(dir, { recursive: true });
}

async function copyFile(src, dest) {
  await ensureDir(path.dirname(dest));
  await fs.copyFile(src, dest);
}

function getLocalBinPath() {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const pkgRoot = path.resolve(here, '..');
  return path.join(pkgRoot, 'node_modules', '.bin');
}

async function seedBackendEntry(workspace) {
  const assets = await backendProvider.getScaffoldAssets();
  for (const asset of assets) {
    if (!asset.targetPath.endsWith(path.join('backend', 'index.ts'))) continue;
    const target = path.join(workspace, asset.targetPath);
    await copyFile(asset.sourcePath, target);
  }
}

test('manifest loader honors package overrides', async () => {
  const workspace = await createTempWorkspace();
  await seedBackendEntry(workspace);

  const pkgJson = {
    name: '@demo/backend',
    version: '1.0.0',
    type: 'module',
    webstir: {
      module: {
        contractVersion: '1.0.0',
        name: '@demo/custom',
        version: '2.0.0',
        kind: 'backend',
        capabilities: ['db']
      }
    }
  };
  await fs.writeFile(path.join(workspace, 'package.json'), JSON.stringify(pkgJson, null, 2), 'utf8');

  const env = {
    WEBSTIR_MODULE_MODE: 'build',
    PATH: `${getLocalBinPath()}${path.delimiter}${process.env.PATH ?? ''}`
  };

  const result = await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });
  const moduleManifest = result.manifest.module;

  assert.equal(moduleManifest?.name, '@demo/custom');
  assert.equal(moduleManifest?.version, '2.0.0');
  assert.deepEqual(moduleManifest?.capabilities, ['db']);
});

test('manifest loader falls back to package name/version when no overrides present', async () => {
  const workspace = await createTempWorkspace();
  await seedBackendEntry(workspace);

  const pkgJson = {
    name: '@demo/fallback',
    version: '4.5.6',
    type: 'module'
  };
  await fs.writeFile(path.join(workspace, 'package.json'), JSON.stringify(pkgJson, null, 2), 'utf8');

  const env = {
    WEBSTIR_MODULE_MODE: 'build',
    PATH: `${getLocalBinPath()}${path.delimiter}${process.env.PATH ?? ''}`
  };

  const result = await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });
  const moduleManifest = result.manifest.module;

  assert.equal(moduleManifest?.name, '@demo/fallback');
  assert.equal(moduleManifest?.version, '4.5.6');
});

test('manifest loader merges compiled module definition metadata', async () => {
  const workspace = await createTempWorkspace();
  await seedBackendEntry(workspace);

  const moduleSource = `export const module = {
  manifest: {
    contractVersion: '1.0.0',
    name: '@demo/with-module',
    version: '9.9.9',
    kind: 'backend',
    capabilities: ['search'],
    routes: [],
    views: []
  }
};
`;

  await fs.writeFile(path.join(workspace, 'src', 'backend', 'module.ts'), moduleSource, 'utf8');
  await fs.writeFile(
    path.join(workspace, 'package.json'),
    JSON.stringify({ name: '@demo/fallback-package', version: '0.0.1', type: 'module' }, null, 2),
    'utf8'
  );

  const env = {
    WEBSTIR_MODULE_MODE: 'build',
    PATH: `${getLocalBinPath()}${path.delimiter}${process.env.PATH ?? ''}`
  };

  const result = await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });
  const moduleManifest = result.manifest.module;

  assert.equal(moduleManifest?.name, '@demo/with-module');
  assert.equal(moduleManifest?.version, '9.9.9');
  assert.deepEqual(moduleManifest?.capabilities, ['search']);
  assert.deepEqual(moduleManifest?.capabilities, ['search']);
});

test('scaffold assets expose core backend templates', async () => {
  const assets = await backendProvider.getScaffoldAssets();
  const targetSet = new Set(assets.map((asset) => asset.targetPath));

  const requiredTargets = [
    path.join('src', 'backend', 'tsconfig.json'),
    path.join('src', 'backend', 'index.ts'),
    path.join('src', 'backend', 'module.ts'),
    path.join('src', 'backend', 'server', 'fastify.ts'),
    path.join('src', 'backend', 'auth', 'adapter.ts'),
    path.join('src', 'backend', 'observability', 'logger.ts'),
    path.join('src', 'backend', 'observability', 'metrics.ts'),
    path.join('src', 'backend', 'functions', 'hello', 'index.ts'),
    path.join('src', 'backend', 'jobs', 'nightly', 'index.ts'),
    path.join('src', 'backend', 'jobs', 'runtime.ts'),
    path.join('src', 'backend', 'jobs', 'scheduler.ts'),
    path.join('src', 'backend', 'db', 'connection.ts'),
    path.join('src', 'backend', 'db', 'migrate.ts'),
    path.join('src', 'backend', 'db', 'migrations', '0001-example.ts'),
    path.join('src', 'backend', 'db', 'types.d.ts'),
    path.join('.env.example')
  ];

  for (const target of requiredTargets) {
    assert.ok(targetSet.has(target), `expected scaffold assets to include ${target}`);
  }
});

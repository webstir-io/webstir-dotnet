import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import fssync from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { backendProvider } from '../dist/index.js';

async function createTempWorkspace(prefix = 'webstir-backend-workspace-') {
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

async function hydrateBackendScaffold(workspace) {
  const assets = await backendProvider.getScaffoldAssets();

  for (const asset of assets) {
    const normalized = asset.targetPath.replace(/\\/g, '/');
    if (!normalized.includes('src/backend/')) {
      continue;
    }

    const target = path.join(workspace, asset.targetPath);
    await copyFile(asset.sourcePath, target);
  }
}


function getLocalBinPath() {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const pkgRoot = path.resolve(here, '..');
  return path.join(pkgRoot, 'node_modules', '.bin');
}

test('build mode produces transpiled output and manifest', async () => {
  const workspace = await createTempWorkspace();
  await hydrateBackendScaffold(workspace);

  const bin = getLocalBinPath();
  const env = {
    WEBSTIR_MODULE_MODE: 'build',
    WEBSTIR_BACKEND_TYPECHECK: 'skip',
    PATH: `${bin}${path.delimiter}${process.env.PATH ?? ''}`,
  };

  const result = await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });

  const buildRoot = path.join(workspace, 'build', 'backend');
  const outFile = path.join(buildRoot, 'index.js');
  assert.equal(fssync.existsSync(outFile), true, 'expected build/backend/index.js to exist');

  assert.ok(Array.isArray(result.manifest.entryPoints));
  assert.ok(result.manifest.entryPoints.some((e) => e.endsWith('index.js')));
});

test('publish mode bundles output and manifest has entry', async () => {
  const workspace = await createTempWorkspace();
  await hydrateBackendScaffold(workspace);

  const bin = getLocalBinPath();
  const env = {
    WEBSTIR_MODULE_MODE: 'publish',
    WEBSTIR_BACKEND_TYPECHECK: 'skip',
    PATH: `${bin}${path.delimiter}${process.env.PATH ?? ''}`,
  };

  const result = await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });

  const buildRoot = path.join(workspace, 'build', 'backend');
  const outFile = path.join(buildRoot, 'index.js');
  assert.equal(fssync.existsSync(outFile), true, 'expected build/backend/index.js to exist');

  assert.ok(result.manifest.entryPoints.length >= 1);
});

test('publish mode emits sourcemaps when opt-in flag is set', async () => {
  const workspace = await createTempWorkspace();
  await hydrateBackendScaffold(workspace);

  const bin = getLocalBinPath();
  const env = {
    WEBSTIR_MODULE_MODE: 'publish',
    WEBSTIR_BACKEND_SOURCEMAPS: 'on',
    WEBSTIR_BACKEND_TYPECHECK: 'skip',
    PATH: `${bin}${path.delimiter}${process.env.PATH ?? ''}`,
  };

  const result = await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });

  const buildRoot = path.join(workspace, 'build', 'backend');
  const mapFile = path.join(buildRoot, 'index.js.map');
  assert.equal(fssync.existsSync(mapFile), true, 'expected build/backend/index.js.map to exist');
  assert.ok(
    result.artifacts.some((artifact) => artifact.path.endsWith('index.js.map') && artifact.type === 'asset'),
    'expected index.js.map to be included as an asset artifact'
  );
});

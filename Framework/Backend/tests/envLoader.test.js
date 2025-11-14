import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

import { backendProvider } from '../dist/index.js';

async function createTempWorkspace(prefix = 'webstir-backend-env-') {
  const dir = await fs.mkdtemp(path.join(os.tmpdir(), prefix));
  return dir;
}

async function copyFile(src, dest) {
  await fs.mkdir(path.dirname(dest), { recursive: true });
  await fs.copyFile(src, dest);
}

function getLocalBinPath() {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const pkgRoot = path.resolve(here, '..');
  return path.join(pkgRoot, 'node_modules', '.bin');
}

test('env loader reads .env files and surfaces typed config', async () => {
  const workspace = await createTempWorkspace();
  const assets = await backendProvider.getScaffoldAssets();
  for (const asset of assets) {
    await copyFile(asset.sourcePath, path.join(workspace, asset.targetPath));
  }
  await fs.writeFile(
    path.join(workspace, 'package.json'),
    JSON.stringify(
      {
        name: '@demo/env-loader',
        version: '0.0.0',
        type: 'module'
      },
      null,
      2
    ),
    'utf8'
  );

  const envContents = `NODE_ENV=development\nPORT=5055\nAPI_BASE_URL=https://api.example.com\n`;
  await fs.writeFile(path.join(workspace, '.env'), envContents, 'utf8');

  const env = {
    WEBSTIR_MODULE_MODE: 'build',
    WEBSTIR_BACKEND_TYPECHECK: 'skip',
    NODE_OPTIONS: '--experimental-transform-types',
    PATH: `${getLocalBinPath()}${path.delimiter}${process.env.PATH ?? ''}`
  };

  await backendProvider.build({ workspaceRoot: workspace, env, incremental: false });

  const builtEnvModule = path.join(workspace, 'build', 'backend', 'env.js');
  const mod = await import(pathToFileURL(builtEnvModule).href);
  const loaded = mod.loadEnv();

  assert.equal(loaded.PORT, 5055);
  assert.equal(loaded.API_BASE_URL, 'https://api.example.com');
});

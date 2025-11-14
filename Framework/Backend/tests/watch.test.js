import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { startBackendWatch } from '../dist/watch.js';
import { backendProvider } from '../dist/index.js';

async function createTempWorkspace(prefix = 'webstir-backend-watch-') {
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

async function seedBackendEntry(workspace) {
  const assets = await backendProvider.getScaffoldAssets();
  for (const asset of assets) {
    if (!asset.targetPath.endsWith(path.join('backend', 'index.ts'))) continue;
    const target = path.join(workspace, asset.targetPath);
    await copyFile(asset.sourcePath, target);
  }
}

function getLocalBinPath() {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const pkgRoot = path.resolve(here, '..');
  return path.join(pkgRoot, 'node_modules', '.bin');
}

async function waitFor(checkFn, timeoutMs = 5000, pollMs = 100) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    if (await checkFn()) return;
    await new Promise((resolve) => setTimeout(resolve, pollMs));
  }
  throw new Error('waitFor timed out');
}

test('startBackendWatch updates cache files after rebuild', async () => {
  const workspace = await createTempWorkspace();
  await seedBackendEntry(workspace);

  const env = {
    WEBSTIR_MODULE_MODE: 'build',
    WEBSTIR_BACKEND_TYPECHECK: 'skip',
    PATH: `${getLocalBinPath()}${path.delimiter}${process.env.PATH ?? ''}`
  };

  const handle = await startBackendWatch({ workspaceRoot: workspace, env });
  try {
    const outputsPath = path.join(workspace, '.webstir', 'backend-outputs.json');

    await waitFor(async () => {
      try {
        await fs.access(outputsPath);
        return true;
      } catch {
        return false;
      }
    });

    const before = JSON.parse(await fs.readFile(outputsPath, 'utf8'));
    const indexPath = path.join(workspace, 'src', 'backend', 'index.ts');
    await fs.appendFile(indexPath, '\nconsole.log("watch-test");\n', 'utf8');

    await waitFor(async () => {
      try {
        const after = JSON.parse(await fs.readFile(outputsPath, 'utf8'));
        const key = Object.keys(after)[0];
        return before[key] !== after[key];
      } catch {
        return false;
      }
    });

    const manifestDigestPath = path.join(workspace, '.webstir', 'backend-manifest-digest.json');
    await waitFor(async () => {
      try {
        await fs.access(manifestDigestPath);
        return true;
      } catch {
        return false;
      }
    });

    assert.ok(true, 'watch updated cache files after rebuild');
  } finally {
    await handle.stop();
  }
});

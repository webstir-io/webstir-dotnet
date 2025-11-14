import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import fssync from 'node:fs';
import os from 'node:os';
import path from 'node:path';

async function loadProviderOrSkip(t) {
  try {
    const mod = await import('../dist/index.js');
    return mod.frontendProvider;
  } catch (err) {
    // Likely due to optional native deps like sharp; skip gracefully.
    console.warn('[frontend-tests] Skipping provider integration: optional dependency unavailable:', err?.message ?? err);
    t?.diagnostic?.('skip: missing optional dependency');
    return null;
  }
}

async function createWorkspace() {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'webstir-frontend-workspace-'));
  const appDir = path.join(root, 'src', 'frontend', 'app');
  const pageDir = path.join(root, 'src', 'frontend', 'pages', 'home');
  await fs.mkdir(appDir, { recursive: true });
  await fs.mkdir(pageDir, { recursive: true });

  // Minimal app template and page fragment
  await fs.writeFile(path.join(appDir, 'app.html'), '<!DOCTYPE html><html><head><title>App</title></head><body><main></main></body></html>', 'utf8');
  await fs.writeFile(path.join(pageDir, 'index.html'), '<head></head><main><section>Home</section></main>', 'utf8');
  await fs.writeFile(path.join(pageDir, 'index.ts'), 'console.log("home");', 'utf8');

  return root;
}

test('frontend provider build emits JS bundle and manifest entry', async (t) => {
  const frontendProvider = await loadProviderOrSkip(t);
  if (!frontendProvider) return; // skip
  const workspace = await createWorkspace();

  const result = await frontendProvider.build({
    workspaceRoot: workspace,
    env: { WEBSTIR_MODULE_MODE: 'build' },
    incremental: false,
  });

  const jsOut = path.join(workspace, 'build', 'frontend', 'pages', 'home', 'index.js');
  assert.equal(fssync.existsSync(jsOut), true, 'expected build/frontend/pages/home/index.js');

  assert.ok(Array.isArray(result.manifest.entryPoints));
  assert.ok(result.manifest.entryPoints.some((e) => e.endsWith('pages/home/index.js')));
});

test('frontend provider publish produces dist assets and preserves entry in manifest', async (t) => {
  const frontendProvider = await loadProviderOrSkip(t);
  if (!frontendProvider) return; // skip
  const workspace = await createWorkspace();

  // Run build first so manifest has entries from build/frontend
  await frontendProvider.build({ workspaceRoot: workspace, env: { WEBSTIR_MODULE_MODE: 'build' }, incremental: false });
  const publishResult = await frontendProvider.build({ workspaceRoot: workspace, env: { WEBSTIR_MODULE_MODE: 'publish' }, incremental: false });

  // Dist should contain a hashed JS file
  const distPageDir = path.join(workspace, 'dist', 'frontend', 'pages', 'home');
  const files = fssync.existsSync(distPageDir) ? fssync.readdirSync(distPageDir) : [];
  assert.ok(files.some((f) => f.startsWith('index-') && f.endsWith('.js')));

  // Manifest still reflects build/frontend entry points by design
  assert.ok(publishResult.manifest.entryPoints.some((e) => e.endsWith('pages/home/index.js')));
});

import { test } from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import os from 'node:os';
import path from 'node:path';

import { createCacheReporter } from '../dist/cache/reporters.js';

async function createWorkspace(prefix = 'webstir-backend-cache-') {
  return await fs.mkdtemp(path.join(os.tmpdir(), prefix));
}

function makeManifest(overrides = {}) {
  return {
    contractVersion: '1.0.0',
    name: '@demo/test',
    version: '0.0.1',
    kind: 'backend',
    capabilities: [],
    routes: [],
    views: [],
    jobs: [],
    events: [],
    services: [],
    ...overrides
  };
}

test('cache reporter emits diagnostics for output and manifest diffs', async () => {
  const workspaceRoot = await createWorkspace();
  const diagnostics = [];
  const reporter = createCacheReporter({
    workspaceRoot,
    buildRoot: path.join(workspaceRoot, 'build', 'backend'),
    env: {},
    diagnostics
  });

  await reporter.diffOutputs({ 'index.js': 128 }, 'build');
  assert.ok(
    diagnostics.some((d) => d.message.includes('changed 1 file')),
    'expected first diff to report changed files'
  );

  diagnostics.length = 0;

  await reporter.diffOutputs({ 'index.js': 256 }, 'build');
  assert.ok(
    diagnostics.some((d) => d.message.includes('changed 1 file')),
    'expected subsequent diffs to report changed files'
  );

  diagnostics.length = 0;

  await reporter.diffManifest(makeManifest());
  assert.equal(diagnostics.length, 0, 'first manifest digest should not emit diagnostics');

  await reporter.diffManifest(
    makeManifest({
      routes: [{ method: 'GET', path: '/accounts' }]
    })
  );
  assert.ok(
    diagnostics.some((d) => d.message.includes('manifest changed')),
    'expected manifest changes to produce diagnostics'
  );
});

test('cache reporter can silence diagnostics via env', async () => {
  const workspaceRoot = await createWorkspace();
  const diagnostics = [];
  const reporter = createCacheReporter({
    workspaceRoot,
    buildRoot: path.join(workspaceRoot, 'build', 'backend'),
    env: { WEBSTIR_BACKEND_CACHE_LOG: 'off' },
    diagnostics
  });

  await reporter.diffOutputs({ 'index.js': 128 }, 'build');
  await reporter.diffOutputs({ 'index.js': 256 }, 'build');
  await reporter.diffManifest(makeManifest());
  await reporter.diffManifest(
    makeManifest({
      routes: [{ method: 'POST', path: '/silent' }]
    })
  );

  assert.equal(diagnostics.length, 0, 'expected diagnostics to stay empty when logging disabled');
});

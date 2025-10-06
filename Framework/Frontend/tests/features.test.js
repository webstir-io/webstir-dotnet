import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';

import { buildConfig } from '../dist/config/workspace.js';

async function createWorkspace(frontendConfig) {
    const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'webstir-frontend-'));
    const workspaceRoot = path.join(tempRoot, 'workspace');
    const frontendRoot = path.join(workspaceRoot, 'src', 'frontend');
    await fs.mkdir(frontendRoot, { recursive: true });

    if (frontendConfig !== undefined) {
        const configPath = path.join(frontendRoot, 'frontend.config.json');
        await fs.writeFile(configPath, JSON.stringify(frontendConfig, null, 2), 'utf8');
    }

    return {
        workspaceRoot,
        cleanup: () => fs.rm(tempRoot, { recursive: true, force: true })
    };
}

test('buildConfig returns defaults when frontend.config.json is absent', async (t) => {
    const workspace = await createWorkspace();
    t.after(workspace.cleanup);

    const config = buildConfig(workspace.workspaceRoot);
    assert.equal(config.features.htmlSecurity, true);
    assert.equal(config.features.imageOptimization, true);
    assert.equal(config.features.precompression, true);
});

test('buildConfig applies overrides from nested features key', async (t) => {
    const workspace = await createWorkspace({
        features: {
            htmlSecurity: false,
            precompression: false
        }
    });
    t.after(workspace.cleanup);

    const config = buildConfig(workspace.workspaceRoot);
    assert.equal(config.features.htmlSecurity, false);
    assert.equal(config.features.precompression, false);
    assert.equal(config.features.imageOptimization, true);
});

test('buildConfig accepts top-level feature flags', async (t) => {
    const workspace = await createWorkspace({
        htmlSecurity: false,
        imageOptimization: false,
        precompression: true
    });
    t.after(workspace.cleanup);

    const config = buildConfig(workspace.workspaceRoot);
    assert.equal(config.features.htmlSecurity, false);
    assert.equal(config.features.imageOptimization, false);
    assert.equal(config.features.precompression, true);
});

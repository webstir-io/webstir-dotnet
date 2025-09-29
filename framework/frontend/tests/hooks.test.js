import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';

import { runBuild } from '../dist/operations.js';

async function createWorkspaceWithHooks() {
    const tempRoot = await fs.mkdtemp(path.join(os.tmpdir(), 'webstir-hooks-'));
    const workspaceRoot = path.join(tempRoot, 'workspace');
    const appDir = path.join(workspaceRoot, 'src', 'frontend', 'app');
    const pageDir = path.join(workspaceRoot, 'src', 'frontend', 'pages', 'home');

    await fs.mkdir(appDir, { recursive: true });
    await fs.mkdir(pageDir, { recursive: true });

    await fs.writeFile(path.join(appDir, 'app.html'), '<!DOCTYPE html><html><head></head><body><main></main></body></html>', 'utf8');
    await fs.writeFile(path.join(pageDir, 'index.html'), '<head></head><main><section>Home</section></main>', 'utf8');
    await fs.writeFile(path.join(pageDir, 'index.ts'), 'console.log("home");', 'utf8');
    await fs.writeFile(path.join(pageDir, 'index.css'), 'body { color: blue; }', 'utf8');

    const packageJson = {
        name: 'webstir-hooks-fixture',
        version: '0.0.0',
        private: true,
        type: 'module'
    };
    await fs.writeFile(path.join(workspaceRoot, 'package.json'), JSON.stringify(packageJson, null, 2), 'utf8');

    const hookConfig = `import fs from 'node:fs/promises';\nimport path from 'node:path';\n\nasync function record(event, context) {\n  const logPath = path.join(context.workspaceRoot, 'hook-log.json');\n  const payload = JSON.stringify({ event, mode: context.mode, builder: context.builderName ?? null });\n  await fs.appendFile(logPath, payload + '\\n', 'utf8');\n}\n\nexport default {\n  hooks: {\n    pipeline: {\n      beforeAll: (context) => record('pipeline-before', context),\n      afterAll: (context) => record('pipeline-after', context)\n    },\n    builders: {\n      javascript: {\n        before: (context) => record('javascript-before', context),\n        after: (context) => record('javascript-after', context)\n      }\n    }\n  }\n};\n`;
    await fs.writeFile(path.join(workspaceRoot, 'webstir.config.js'), hookConfig, 'utf8');

    return {
        workspaceRoot,
        cleanup: () => fs.rm(tempRoot, { recursive: true, force: true })
    };
}

test('pipeline hooks execute in order', async (t) => {
    const workspace = await createWorkspaceWithHooks();
    t.after(workspace.cleanup);

    await runBuild({ workspaceRoot: workspace.workspaceRoot });

    const logPath = path.join(workspace.workspaceRoot, 'hook-log.json');
    const raw = await fs.readFile(logPath, 'utf8');
    const entries = raw.trim().split('\n').map((line) => JSON.parse(line));

    assert.equal(entries.length, 4);
    assert.deepEqual(entries.map((entry) => entry.event), [
        'pipeline-before',
        'javascript-before',
        'javascript-after',
        'pipeline-after'
    ]);
    assert(entries.every((entry) => entry.mode === 'build'));
    assert.equal(entries[1].builder, 'javascript');
    assert.equal(entries[2].builder, 'javascript');
});

import fs from 'node:fs/promises';
import path from 'node:path';
import os from 'node:os';
import { frontendProvider } from '../dist/index.js';

async function createWorkspace() {
  const root = await fs.mkdtemp(path.join(os.tmpdir(), 'webstir-frontend-smoke-'));
  const appDir = path.join(root, 'src', 'frontend', 'app');
  const pageDir = path.join(root, 'src', 'frontend', 'pages', 'home');
  await fs.mkdir(appDir, { recursive: true });
  await fs.mkdir(pageDir, { recursive: true });
  await fs.writeFile(path.join(appDir, 'app.html'), '<!DOCTYPE html><html><head><title>App</title></head><body><main></main></body></html>', 'utf8');
  await fs.writeFile(path.join(pageDir, 'index.html'), '<head></head><main><section>Home</section></main>', 'utf8');
  await fs.writeFile(path.join(pageDir, 'index.ts'), 'console.log("home")', 'utf8');
  return root;
}

async function main() {
  const workspace = await createWorkspace();
  console.info('[smoke:frontend] build mode');
  const build = await frontendProvider.build({ workspaceRoot: workspace, env: { WEBSTIR_MODULE_MODE: 'build' }, incremental: false });
  console.info('[smoke:frontend] build entries:', build.manifest.entryPoints);
  console.info('[smoke:frontend] build diagnostics:', build.manifest.diagnostics.map(d => d.message));

  console.info('[smoke:frontend] publish mode');
  const publish = await frontendProvider.build({ workspaceRoot: workspace, env: { WEBSTIR_MODULE_MODE: 'publish' }, incremental: false });
  console.info('[smoke:frontend] publish entries:', publish.manifest.entryPoints);
  console.info('[smoke:frontend] publish diagnostics:', publish.manifest.diagnostics.map(d => d.message));
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});


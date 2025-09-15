# Publish

Produce optimized, fingerprinted assets in `dist/` and rewrite HTML to reference them. Intended for production deploys and the Docker sandbox.

## Purpose
- Create lean, cache-friendly assets with stable HTML references.
- Validate production routing and manifests.

## When To Use
- Before deploying or validating with the Docker sandbox.

## CLI
- `webstir publish`

## Steps
1. Run frontend and backend pipelines in production mode.
2. Use **esbuild** to bundle, tree-shake, minify JavaScript, and perform **automatic code-splitting** (10-100x faster).
3. esbuild handles content hashing for JavaScript: `index-<hash>.js` for entries, `chunks/*-<hash>.js` for shared code.
4. Webstir fingerprints CSS assets: `index.<hash>.css`.
5. Minify HTML/CSS and remove comments and source maps.
6. Generate per-page `manifest.json` with entry point mappings.
7. Rewrite page HTML to reference fingerprinted assets from the manifest.
8. Optimize and copy Images, Fonts, and Media to `dist/frontend/{images|fonts|media}/**`.

## Outputs
- `dist/frontend/pages/<page>/index.html`
- `dist/frontend/pages/<page>/index-<hash>.js` (entry point)
- `dist/frontend/pages/<page>/index.<hash>.css`
- `dist/frontend/pages/<page>/manifest.json`
- `dist/frontend/chunks/*-<hash>.js` (shared code chunks)
- Static assets: `dist/frontend/{images|fonts|media}/**`
- Shared app assets under `dist/frontend/app/*`

## Notes
- **Code-splitting**: esbuild automatically extracts shared dependencies into chunks. The browser loads these transparently via ESM imports—no manual chunk management needed.
- Dynamic imports are not inlined by the bundler in v1. If your page (or app) uses `import('...')`, the call remains at runtime. For assets under the app shell, use absolute paths (e.g., `await import('/app/router.js')`) so they resolve correctly after publish.
- JavaScript minification via esbuild includes identifier mangling, dead code elimination, and console stripping.

## Errors & Exit Codes
- Non-zero on pipeline failures or missing inputs.
- Logs indicate which page or asset failed.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Pipelines — [pipelines](../explanations/pipelines.md)
- Servers (sandbox) — [servers](../explanations/servers.md)
- Sandbox — [sandbox](sandbox.md)
- Engine — [engine](../explanations/engine.md)

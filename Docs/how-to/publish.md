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
2. Fingerprint assets per page: `index.<timestamp>.{css|js}`.
3. Minify HTML/CSS/JS and remove comments and source maps.
4. Generate per-page `manifest.json`.
5. Rewrite page HTML to reference fingerprinted assets from the manifest.
6. Copy Images, Fonts, and Media to `dist/frontend/{images|fonts|media}/**`.

## Outputs
- `dist/frontend/pages/<page>/index.html`
- `dist/frontend/pages/<page>/index.<timestamp>.{css|js}`
- `dist/frontend/pages/<page>/manifest.json`
- Static assets: `dist/frontend/{images|fonts|media}/**`
- Shared app assets under `dist/frontend/app/*`

## Errors & Exit Codes
- Non-zero on pipeline failures or missing inputs.
- Logs indicate which page or asset failed.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Pipelines — [pipelines](../explanations/pipelines.md)
- Servers (sandbox) — [servers](../explanations/servers.md)
- Sandbox — [sandbox](sandbox.md)
- Engine — [engine](../explanations/engine.md)

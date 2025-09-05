# Publish

Produce optimized, fingerprinted assets in `dist/` and rewrite HTML to reference them. Intended for production deploys and the Docker sandbox.

## Purpose
- Create lean, cache‑friendly assets with stable HTML references.
- Validate production routing and manifests.

## When To Use
- Before deploying or validating with the Docker sandbox.

## CLI
- `webstir publish`

## Steps
1. Run client and server pipelines in production mode.
2. Fingerprint assets per page: `index.<timestamp>.{css|js}`.
3. Minify HTML/CSS/JS and remove comments and source maps.
4. Generate per‑page `manifest.json`.
5. Rewrite page HTML to reference fingerprinted assets from the manifest.

## Outputs
- `dist/client/pages/<page>/index.html`
- `dist/client/pages/<page>/index.<timestamp>.{css|js}`
- `dist/client/pages/<page>/manifest.json`
- Shared app assets under `dist/client/app/*`

## Errors & Exit Codes
- Non‑zero on pipeline failures or missing inputs.
- Logs indicate which page or asset failed.

## Related Docs
- Workflows — [workflows](../workflows.md)
- Pipelines — [pipelines](../../pipelines/pipelines.md)
- Servers (sandbox) — [servers](../../solution/servers/servers.md)
- Sandbox — [sandbox](../../solution/sandbox/sandbox.md)
- Engine — [engine](../../solution/engine/engine.md)

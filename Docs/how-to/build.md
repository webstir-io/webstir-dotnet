# Build

Compile and stage the app for development. Processes client HTML/CSS/TS and compiles the server into `build/`.

## Purpose
- Produce up-to-date dev outputs without optimization.
- Validate the workspace and surface actionable errors.

## When To Use
- Before running tests locally.
- In CI to check that code compiles.

## CLI
- `webstir build [--clean]`

## Steps
1. Validate workspace structure and required inputs.
2. Compile TypeScript for client/server using the embedded base config.
3. Process CSS imports and copy assets.
4. Merge page HTML into `src/client/app/app.html` and write to `build/client/pages/<page>/index.html`.
5. Copy app assets to `build/client/app/`.

## Outputs
- `build/client/**` with readable assets and page HTML.
- `build/server/index.js` compiled server entry.

## Flags
- `--clean`: remove previous `build/**` before building.

## Errors & Exit Codes
- Non-zero on TypeScript errors, missing base HTML, or pipeline failures.
- Logs identify the failing stage and file when possible.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Engine — [engine](../explanations/engine.md)
- Pipelines — [pipelines](../explanations/pipelines.md)
- Workspace — [workspace](../explanations/workspace.md)
- Servers — [servers](../explanations/servers.md)
- Tests — [tests](../explanations/testing.md)

# Pipelines

Build and publish stages for HTML, CSS, and JS/TS. These pipelines run under the Engine and are orchestrated by workflows (`build`, `watch`, `publish`).

## Goals
- Deterministic outputs for given inputs.
- Fast dev builds; optimized production bundles.
- Clear errors that name the stage and file.

## Modes
- Dev (build/watch): readable output in `build/**`, source context preserved, no minification.
- Publish: optimized output in `dist/**`, fingerprinted asset names, HTML rewritten to point at the manifest.

## HTML
- Source: page fragments under `src/client/pages/<page>/index.html`.
- Base template: `src/client/app/app.html` must contain a `<main>` placeholder.
- Dev: merge page HTML into the base template → `build/client/pages/<page>/index.html`.
- Publish: minify and rewrite asset references using the per-page `manifest.json`.
- Errors: fail if base HTML is missing or lacks `<main>`.

## CSS
- Resolves `@import` and asset URLs; copies referenced assets.
- Dev: keep readable, unminified CSS.
- Publish: autoprefix and minify; optional CSS Modules; write fingerprinted `index.<timestamp>.css`.
- Output paths follow the page layout in `build/client/pages/<page>/` and `dist/client/pages/<page>/`.

## JavaScript / TypeScript
- Compiler: `tsc --build` using an embedded base `tsconfig`.
- Graph: ESM only.
- Dev: produce readable JS under `build/client/**` and `build/server/**`.
- Publish: tree-shake and minify; write fingerprinted `index.<timestamp>.js` per page; emit manifest entries.

## Manifests
- One `manifest.json` per page under `dist/client/pages/<page>/` listing fingerprinted assets.
- HTML is rewritten in publish to reference manifest entries (stable HTML URLs).

## Outputs
- Dev: `build/client/**`, `build/server/**`.
- Publish: `dist/client/pages/<page>/index.html`, `index.<timestamp>.{css|js}`, `manifest.json`.

## Related Docs
- Engine internals — engine.md
- Workflows — ../reference/workflows.md
- Servers (dev) — servers.md
- Dev service — devservice.md
- Workspace & paths — workspace.md

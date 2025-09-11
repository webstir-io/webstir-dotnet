# Pipelines

Build and publish stages for HTML, CSS, JS/TS, and static assets (Images, Fonts, Media). These pipelines run under the Engine and are orchestrated by workflows (`build`, `watch`, `publish`).

## Goals
- Deterministic outputs for given inputs.
- Fast dev builds; optimized production bundles.
- Clear errors that name the stage and file.

## Modes
- Dev (build/watch): readable output in `build/**`, source context preserved, no minification.
- Publish: optimized output in `dist/**`, fingerprinted asset names, HTML rewritten to point at the manifest.

## HTML
- Source: page fragments under `src/frontend/pages/<page>/index.html`.
- Base template: `src/frontend/app/app.html` must contain a `<main>` placeholder.
- Dev: merge page HTML into the base template → `build/frontend/pages/<page>/index.html`.
- Publish: rewrite asset references using the per-page `manifest.json`,
  run HTML minifier (removes comments, collapses inter-tag whitespace,
  safe attribute optimizations), and emit precompressed `.html.br/.html.gz`.
- Errors: fail if base HTML is missing or lacks `<main>`.

## CSS
- Resolves `@import` and asset URLs; copies referenced assets.
- Dev: keep readable, unminified CSS.
- Publish: autoprefix and minify; optional CSS Modules; write fingerprinted `index.<timestamp>.css`.
- Minifier: token-aware (preserves strings/urls); trims whitespace; normalizes numbers/zeros; shortens hex (incl. `#rrggbbaa→#rgba`); enforces spacing around `and/or/not` in `@media/@supports`; collapses zero shorthands (`margin/padding/inset`).
- Legacy removal: strips `-ms-`, `-o-`, `-khtml-` prefixed declarations and legacy flexbox values; keeps a short allowlist of modern `-webkit-*` declarations.
- Output paths follow the page layout in `build/frontend/pages/<page>/` and `dist/frontend/pages/<page>/`.

## JavaScript / TypeScript
- Compiler: `tsc --build` using an embedded base `tsconfig`.
- Graph: ESM only.
- Dev: produce readable JS under `build/frontend/**` and `build/backend/**`.
- Publish: tree-shake and minify; write fingerprinted `index.<timestamp>.js` per page; emit manifest entries.

## Assets (Images / Fonts / Media)
- Source folders under `src/frontend/`:
  - Images: `images/**` (png, jpg, jpeg, gif, svg, webp, ico)
  - Fonts: `fonts/**` (woff2, woff, ttf, otf, eot, svg)
  - Media: `media/**` (mp3, m4a, wav, ogg, mp4, webm, mov)
- Dev: copy to `build/frontend/{images|fonts|media}/**`, preserving structure.
- Publish: copy from `build/**` to `dist/frontend/{images|fonts|media}/**`, preserving structure.
- No transforms or fingerprinting in v1; safe no-ops when folders are absent.

## Manifests
- One `manifest.json` per page under `dist/frontend/pages/<page>/` listing fingerprinted assets.
- HTML is rewritten in publish to reference manifest entries (stable HTML URLs).

## Outputs
- Dev: `build/frontend/**`, `build/backend/**`.
- Publish: `dist/frontend/pages/<page>/index.html`, `index.<timestamp>.{css|js}`, `manifest.json`.

## Related Docs
- Engine internals — engine.md
- Workflows — ../reference/workflows.md
- Servers (dev) — servers.md
- Dev service — devservice.md
- Workspace & paths — workspace.md

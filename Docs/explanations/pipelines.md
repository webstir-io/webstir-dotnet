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
  safe attribute optimizations), and emit precompressed `.html.br`.
- Errors: fail if base HTML is missing or lacks `<main>`.

## CSS
- **Bundler**: esbuild handles CSS processing alongside JavaScript for unified pipeline.
- Dev: esbuild bundles with `--sourcemap` for debugging; outputs readable CSS.
- Publish: esbuild minifies CSS with `--minify`; writes fingerprinted `index-<hash>.css`.
- Features:
  - Automatic `@import` resolution and bundling
  - CSS Modules support via `.module.css` extension
  - URL rewriting for assets (images, fonts)
  - Modern CSS syntax support including nesting
- esbuild configuration:
  - Development: `--bundle --sourcemap --loader:.css=css`
  - Production: `--bundle --minify --loader:.css=css --entry-names=[dir]/index-[hash]`
- Output paths follow the page layout in `build/frontend/pages/<page>/` and `dist/frontend/pages/<page>/`.

## JavaScript / TypeScript
- Compiler: `tsc --build` using an embedded base `tsconfig` for type checking.
- Bundler: **esbuild** for exceptional performance (10-100x faster than traditional bundlers).
- Dev: produce readable JS with source maps under `build/frontend/**`; esbuild bundles with `--sourcemap`.
- Publish: esbuild handles tree-shaking, minification, console stripping, and **automatic code-splitting**; write fingerprinted `index-<hash>.js` per page entry.
- Output shape: ESM bundle format, referenced with `type="module"` for modern browser compatibility.
- esbuild configuration:
  - Development: `--bundle --sourcemap --format=esm --define:process.env.NODE_ENV="development"`
  - Production: `--bundle --minify --format=esm --drop:console --splitting --chunk-names=chunks/[name]-[hash] --entry-names=[dir]/index-[hash] --metafile --define:process.env.NODE_ENV="production"`
- Code-splitting (production): Automatically extracts shared dependencies into `chunks/` folder; entry points import chunks via ESM; browser handles chunk loading transparently.
- Content hashing: esbuild manages hashes for both entries and chunks via `--entry-names` and `--chunk-names` patterns.
- Entry points: Automatically discovered from `build/frontend/pages/*/index.js` (tsc output).

## Assets (Images / Fonts / Media)
- Source folders under `src/frontend/`:
  - Images: `images/**` (png, jpg, jpeg, gif, svg, webp, ico)
  - Fonts: `fonts/**` (woff2, woff, ttf, otf, eot, svg)
  - Media: `media/**` (mp3, m4a, wav, ogg, mp4, webm, mov)
- Dev: copy to `build/frontend/{images|fonts|media}/**`, preserving structure.
- Publish: optimize and copy to `dist/frontend/{images|fonts|media}/**`.
  - Images: `ImageOptimizer` compresses and generates WebP/AVIF variants when tools available.
  - Fonts: `FontOptimizer` converts to WOFF2 when possible, enforces `font-display: swap`.
- Graceful fallback: optimization tools are optional; originals served if tools unavailable.

## Manifests
- One `manifest.json` per page under `dist/frontend/pages/<page>/` listing fingerprinted assets.
- HTML is rewritten in publish to reference manifest entries (stable HTML URLs).

## Outputs
- Dev: `build/frontend/**`, `build/backend/**`.
- Publish:
  - Pages: `dist/frontend/pages/<page>/index.html`, `index-<hash>.js`, `index.<hash>.css`, `manifest.json`
  - Shared chunks: `dist/frontend/chunks/*-<hash>.js` (vendor libraries, common code)

## Hooks
- Optional `webstir.config.js` at the workspace root can inject pre/post logic.
- Pipeline hooks: `beforeAll`/`afterAll` around the entire build or publish run.
- Builder hooks: `before`/`after` per stage (`javascript`, `css`, `html`, `static-assets`).
- Handlers receive `{ config, mode, workspaceRoot, builderName?, changedFile? }`.
- See [Extend Pipelines with Hooks](../how-to/pipeline-hooks.md) for usage details.

## Related Docs
- Engine internals — engine.md
- Workflows — ../reference/workflows.md
- Servers (dev) — servers.md
- Dev service — devservice.md
- Workspace & paths — workspace.md

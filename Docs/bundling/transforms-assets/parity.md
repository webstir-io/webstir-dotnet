# Transforms & Assets

## Current (Webstir)
- TS build: runs `tsc` with project refs; writes to `build/client` (`Engine/Pipelines/JavaScript/Build/JsBuilder.cs`).
- JS publish: regex‑based import/export stripping, simple hoisting, basic minifier; no Babel/SWC; no JSX/TS in publish (`Engine/Pipelines/JavaScript/Publish/JsTransformer.cs`).
- CSS: CSS Modules‑style scoping with hash suffix, import rewriting, basic autoprefixer and minifier (`Engine/Pipelines/Css/Publish`).
- Assets: copies image files; no hashing/inlining; CSS url() rewriting limited (`Engine/Pipelines/Assets/AssetHandler.cs`).
- JSON imports allowed by resolver, but no dedicated JSON loader/transform.
- Defines/macros (`process.env.*`), compile‑time flags: not implemented.

## Baseline (Others)
- Webpack/Rspack: loader ecosystem (TS/JSX, CSS/PostCSS, assets); powerful asset modules; define plugin; Babel/SWC fast paths.
- Rollup/Vite: plugin pipelines (TS/JSX via esbuild, PostCSS, URL loaders); virtual modules.
- esbuild: integrated TS/JSX/CSS transforms; define macros; asset loader with inlining/URLing.
- Turbopack: SWC‑based transforms; partial loader compatibility; asset handling focused on frameworks.

## Gaps to Parity
- (P1) Production JS transform via esbuild/SWC (targets, JSX, minify, sourcemaps).
- (P2) First‑class JSON, text, and asset imports (file/url/inline loaders with size thresholds).
- (P5) PostCSS pipeline and opt‑in preprocessors (Sass/Less) with source maps.
- (P1) Defines/macros and dead‑branch pruning (`process.env.NODE_ENV`, feature flags).
- (P4) Consistent asset URL rewriting and hashing across JS/CSS/HTML.

## Parity Plan
- Replace/augment publish JS pipeline with esbuild/SWC (transpile + minify + sourcemaps) while keeping current graph.
- Add loader layer: JSON, raw text, assets (url/file/inline with limit), CSS Modules on/off.
- Integrate PostCSS and optional Sass; emit CSS source maps in dev.
- Implement `define` support and tree‑shake dead branches.
- Standardize asset URL pipeline and emit hashed filenames; update HTML/manifest accordingly.

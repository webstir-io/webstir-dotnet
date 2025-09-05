# Output & Packaging

## Current (Webstir)
- Format: concatenated page bundle; no module runtime; ESM required (flags CommonJS) (`Engine/Pipelines/JavaScript/Publish/JsBundler.cs`).
- Source maps: emitted by `tsc` in build; dropped in publish for JS and CSS.
- Naming: timestamped filenames (seconds); no content hashing.
- HTML integration: rewrite to per‑page manifest (JS/CSS); strip refresh script and comments (`Engine/Pipelines/Html/Publish/HtmlBundler.cs`).
- Preload/prefetch: not emitted.

## Baseline (Others)
- Webpack/Rspack: ESM/CJS/IIFE outputs; runtime loader; content hashes; asset manifest; preload/prefetch hints.
- Rollup/Vite: ESM outputs with code splitting; sourcemaps; content hashes; HTML plugin injection.
- esbuild: ESM/CJS/IIFE; content hashes; sourcemaps; splitting/runtime.
- Turbopack: chunked outputs; framework‑integrated HTML/template handling.

## Gaps to Parity
- (P2) Content hashing and stable chunk naming; manifest with full asset graph.
- (P3) Generate source maps for production (opt‑in) and preserve license comments.
- Runtime loader for chunked output; multiple formats if needed.
- (P5) Emit preload/prefetch hints for critical chunks and CSS.

## Parity Plan
- Switch to content hashes for JS/CSS and assets; keep timestamp as fallback for dev only.
- Generate sourcemaps in publish phase; gate with config.
- Add minimal runtime for dynamic import chunks and CSS extraction.
- HTML plugin step to inject preload/prefetch based on graph/manifest.

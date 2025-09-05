# Optimization

## Current (Webstir)
- Tree shaking: export‑usage based, across static imports; no sideEffects field; no deep DCE (`Engine/Pipelines/JavaScript/Publish/JsTreeShaker.cs`).
- Scope hoisting: simple heuristics; avoids dynamic imports and module globals (`JsTransformer.CanHoist`).
- Code splitting: none; one bundle per page; no shared/runtime chunks (`Engine/Pipelines/JavaScript/Publish/JsBundler.cs`).
- Minification: regex‑based JS/CSS minifiers; no compression awareness.
- Pre‑bundle vendors: none.

## Baseline (Others)
- Webpack/Rspack: chunk graph, dynamic import splitting, cache groups, sideEffects + usedExports; terser/SWC minify.
- Rollup/Vite: ESM treeshake, module/entry splitting, dynamic import chunks; terser/esbuild minify.
- esbuild: fast tree‑shake + splitting; minifies with scope/prop mangling.
- Turbopack: incremental compilation/chunking; fast dev; evolving prod story.

## Gaps to Parity
- (P2) Robust tree shaking with sideEffects, pure annotations, and DCE.
- (P2) Configurable code splitting: dynamic imports, shared vendor/runtime chunks.
- Modern minification (SWC/esbuild/terser) with sourcemaps.
- (P3) Dependency pre‑bundle for dev to cut cold start.

## Parity Plan
- Integrate esbuild/SWC for production minification and optional tree shaking.
- Add chunking: detect dynamic imports; build shared chunks; emit loader/runtime.
- Respect `package.json#sideEffects` and `/*#__PURE__*/` for DCE.
- Add optional vendor pre‑bundle on dev startup (flatten large deps).

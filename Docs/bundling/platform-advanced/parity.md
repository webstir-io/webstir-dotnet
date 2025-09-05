# Platform & Advanced

## Current (Webstir)
- Fullstack layout with Node server runner; no SSR/SSG bundling pipeline.
- No workers/service worker bundling helpers.
- No WASM pipeline.
- No module federation/remote loading.
- No automatic polyfills.

## Baseline (Others)
- Webpack/Rspack: SSR builds, worker targets, wasm loader, federation, polyfill plugins.
- Rollup/Vite: SSR/SSG modes; worker bundling; wasm loaders; polyfill plugins.
- esbuild: worker target, wasm loader, inject/polyfill; no federation.
- Turbopack: strong SSR/SSG for Next.js; evolving worker/wasm support.

## Gaps to Parity
- (P7) SSR/SSG client+server split builds and hydration helpers.
- (P7) Worker and service worker builds with registration utilities.
- (P7) WASM asset pipeline and async loader glue.
- (P8) Optional polyfills based on target/feature usage.
- (P8) Remote module loading/federation (optional).

## Parity Plan
- Add SSR/SSG mode that emits server bundle + client assets with manifest/hydration map.
- Add `target: worker` pipeline and service worker build + registration scaffold.
- Implement wasm bundling (copy/inline) with JS loader and MIME handling.
- Provide target‑driven polyfill inject (or document opt‑out strategy).
- Explore lightweight remote loading for micro‑frontends (later phase).

# JS Source Maps — Plan

## Goals
- Solid dev debugging (TypeScript → browser).
- No source maps in production by default.
- Optional production maps for internal diagnostics.

## Current State
- Dev: `tsc` compiles to `build/**` with `.js.map` (templates enable `sourceMap`). Dev server serves `build/`; maps work.
- Publish: Bundles and minifies JS. Does not emit source maps. `JsPublisher` ignores `.map` files and strips `sourceMappingURL` comments.
- Code exists for a JS map generator but it is not wired into bundling.

## Decisions (No Flags)
- Dev (build/watch): always provide source maps for browser debugging.
- Prod (publish): never emit source maps and always strip `sourceMappingURL` comments.
- No CLI flags or config toggles; behavior is determined by the workflow (dev vs. publish).

## Implementation Plan
Dev
- Keep `sourceMap: true` in the embedded `base.tsconfig.json` so `tsc` writes `.js.map` under `build/**`.
- Set `inlineSources: true` so maps embed original TS (`sourcesContent`) and DevTools can show sources without reading from `src/**`.
- Ensure dev server serves map files from `build/` without special handling.

Publish
- Continue current behavior: bundle, tree‑shake, minify, and write only `index.<ts>.js` to `dist/`.
- Explicitly delete or ignore any `.map` files and strip any `sourceMappingURL` comments (`JsPublisher` already does this for app assets).
- Do not attempt to generate bundle maps until a minifier with map support is introduced.

Future (engine only, no surface flags)
- If we later add a map‑aware minifier, consider generating accurate production maps solely for internal diagnostics and storing them outside `dist/` (e.g., under a private artifacts directory). They remain unpublished and unlinked.

## Risks / Notes
- Without a map‑aware minifier, any attempt at production maps would be inaccurate. We avoid emitting them.
- Hoisting/transforms shift code; dev maps are generated pre‑bundle and remain accurate enough for debugging.

## Test Coverage
- Dev: `.js` files in `build/**` reference and are served with `.js.map`.
- Publish: no `.map` files written to `dist/**` and no `sourceMappingURL` lines present.

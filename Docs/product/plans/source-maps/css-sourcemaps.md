# CSS Source Maps — Plan

## Goals
- Keep dev simple and fast.
- No CSS maps in production by default.
- Optional maps later without breaking publish guarantees.

## Current State
- Dev: CSS imports are rewritten for `build/**`; no bundling. No maps are generated or needed.
- Publish: CSS is bundled, autoprefixed, and minified. No maps are emitted. A `SourceMapGenerator` exists but is unused.

## Decisions (No Flags)
- Dev: do not generate CSS source maps (dev CSS is unbundled and readable).
- Prod (publish): never emit CSS source maps and never append `/*# sourceMappingURL=... */`.
- No CLI flags or config toggles; behavior is determined by the workflow (dev vs. publish).

## Implementation Plan
Dev
- Keep current approach: readable CSS under `build/**`; no need for maps.

Publish
- Keep current behavior: concatenate, autoprefix, minify; write only `index.<ts>.css` to `dist/`.
- Do not emit `.css.map` files and ensure no `sourceMappingURL` comments exist.
- Defer the existing `SourceMapGenerator` until transforms become map‑aware.

Future (engine only, no surface flags)
- When we add a map‑aware autoprefixer/minifier, consider generating accurate maps strictly for internal diagnostics, stored outside `dist/` and never linked.

## Risks / Notes
- Autoprefixing/minification change layout; without map‑aware transforms, maps would be inaccurate. We avoid emitting them.
- Dev CSS is readable; maps add minimal value.

## Test Coverage
- Publish: no `.map` files in `dist/**` and no `sourceMappingURL` comments.

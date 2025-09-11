# CSS Minification — Plan

Modern defaults. No external tools. No configs. Publish-only.

## Goals
- Safe, built-in CSS minification for publish.
- Minimal, modern-only prefixing (drop legacy IE/old flexbox variants).
- Deterministic behavior with no flags or config files.

## Non‑Goals
- Legacy browser support.
- Aggressive rule reordering/merging that can affect cascade/specificity.
- Source maps in publish.

## Defaults (Modern Targets)
- Assume evergreen browsers: latest Chrome, Firefox, Safari, Edge.
- Prefer correctness and simplicity over chasing rare edge cases.

## Approach

### Phase 1 — Harden Current Implementation (fast)
Objective: Harden minifier/prefixer for modern targets and add publish tests.
Status: Done

#### TODO

- [x] Preserve license comments; drop other block comments.
- [x] Tokenizer string/url awareness; leave `url(data:...)` untouched.
- [x] Normalize numbers and zero units; shorten hex colors.
- [x] Modern-only prefixing; exclude legacy flexbox and `-ms-*`.
- [x] Publish tests: strings/URLs, license comments, `calc()` spacing, prefixes.

### Phase 2 — Tokenizer + Serializer (safer core)
Objective: Tokenize/serialize CSS and move minification to token passes.
Status: Done

#### TODO

- [x] Token model + tokenizer for core tokens.
- [x] Serializer: minimal whitespace; drop trailing `;` in blocks.
- [x] Move minification passes to token level.
- [x] Keep prefix→minify order; retain API surface.
- [x] Tests: unit invariants and seed snapshot.

### Phase 3 — Safe Enhancements (optional)
Objective: Add small, safe minification improvements.
Status: Partially Done

#### TODO

- [x] Preserve spaces around `and`/`or`/`not` in media/supports.
- [x] Hex rgba shortener: `#rrggbbaa` → `#rgba` when reducible.
- [x] Collapse zero shorthands where safe (margin/padding/inset).
- [x] Color canonicalization only when unambiguous (e.g., `white`→`#fff`, `black`→`#000`, `fuchsia`→`#f0f`, `yellow`→`#ff0`, `transparent`→`#0000`).
- [x] Strip known legacy prefixes on publish (always on): remove `-ms-`, `-o-`, `-khtml-`, legacy flexbox values; keep select `-webkit-*` used by modern Safari.

### Phase 4 — Transport Compression (publish)
Objective: Generate precompressed CSS at publish using .NET built-ins.
Status: Done

#### TODO

- [x] Publish step: create `.css.br` (Brotli) and `.css.gz` (gzip) alongside originals.
- [x] Implementation: use `BrotliStream` (quality 11) and `GZipStream` (level 6–9); no external tools.
- [x] Scope: compress `.css` only; skip already‑compressed binaries (images, fonts, videos).
- [x] Determinism: stable outputs for identical inputs; avoid on‑the‑fly compression for static assets.
- [x] Docs: serving guidance (`Content-Encoding`, `Vary: Accept-Encoding`); CDN cache key notes. See `docs/how-to/precompression.md`.
- [x] Tests: publish fixtures include `.br/.gz` for CSS; verify presence and that sizes are smaller than originals; confirm binaries are not precompressed.

### Phase 5 — Minimal AST + Prefixer
Objective: Introduce a minimal AST and targeted prefixer only if needed.
Status: Planned

#### TODO

- [ ] Decide AST necessity (YAGNI-friendly call).
- [ ] If needed: define nodes; parse tokens → minimal AST.
- [ ] Integrate declaration-level prefixer; small modern table.
- [ ] Tests: prefixed-before-unprefixed order; no duplicates per block.

## Acceptance Criteria
- Dist CSS has no comments except `/*! ... */` when present.
- String literals and `url(...)` contents are byte‑preserved (modulo added quotes when necessary for validity).
- No `-ms-` or legacy flexbox prefixes are emitted.
- Seed project CSS size reduced by ≥15% vs. unminified build output.
- All new tests pass in publish workflow.
 - Publish outputs precompressed `.css.br` and `.css.gz` for CSS; binaries are not double‑compressed.

## Deliverables
- Tokenizer and serializer under `Engine/Pipelines/Css/*` with unit coverage.
- Updated `CssBundler` to use tokenizer/serializer in publish.
- Slimmed prefix map focused on modern targets.
- Tests: invariants, fixtures, and existing publish checks extended.
- Updated docs: assessment and this plan.
 - Publish compression utility using .NET `BrotliStream`/`GZipStream` and wiring in the publish pipeline.
 - Tests verifying presence of `.br/.gz` artifacts for CSS and skipping binaries.

## Risks & Mitigations
- Parser complexity: keep grammar minimal; focus on publish‑safe transforms.
- Data URI handling: keep as opaque tokens; never transform contents.
- Maintenance of prefix table: keep scope small and modern; revisit only on demand.

## Transport Compression (gzip/Brotli)
- Purpose: shrink transfer size; independent of minification output.
- Default: precompress static `.css` at publish as `.css.br` (Brotli) and `.css.gz` (gzip).
- Serving:
  - Prefer `br` for modern browsers; fall back to `gzip`.
  - Set headers: `Content-Encoding` on precompressed responses and `Vary: Accept-Encoding`.
  - CDN: serve precompressed variants; ensure cache keys vary by encoding. Avoid on‑the‑fly compression for cached static assets.
- Levels: use Brotli level 11 for precompressed artifacts; gzip level 6–9.
- Scope: compress `.css` only. Skip already‑compressed binaries (images, fonts, videos).
- Notes: orthogonal to minification; produce artifacts at publish for deterministic behavior rather than relying on edge settings.
## Rough Timeline
- Phase 1: 1–2 days.
- Phase 2: 1–2 weeks.
- Phase 3: optional, time‑boxed.
- Phase 4: ~1 day.
- Phase 5: 3–5 days.

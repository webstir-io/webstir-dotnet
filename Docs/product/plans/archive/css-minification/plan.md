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

### Phase 3 — Minimal AST + Prefixer
Objective: Introduce a minimal AST and targeted prefixer only if needed.
Status: Planned

#### TODO

- [ ] Decide AST necessity (YAGNI-friendly call).
- [ ] If needed: define nodes; parse tokens → minimal AST.
- [ ] Integrate declaration-level prefixer; small modern table.
- [ ] Tests: prefixed-before-unprefixed order; no duplicates per block.

### Phase 4 — Safe Enhancements (optional)
Objective: Add small, safe minification improvements.
Status: Partially Done

#### TODO

- [x] Preserve spaces around `and`/`or`/`not` in media/supports.
- [x] Hex rgba shortener: `#rrggbbaa` → `#rgba` when reducible.
- [x] Collapse zero shorthands where safe (margin/padding/inset).
- [x] Color canonicalization only when unambiguous (e.g., `white`→`#fff`, `black`→`#000`, `fuchsia`→`#f0f`, `yellow`→`#ff0`, `transparent`→`#0000`).
- [x] Strip known legacy prefixes on publish (always on): remove `-ms-`, `-o-`, `-khtml-`, legacy flexbox values; keep select `-webkit-*` used by modern Safari.

## Acceptance Criteria
- Dist CSS has no comments except `/*! ... */` when present.
- String literals and `url(...)` contents are byte‑preserved (modulo added quotes when necessary for validity).
- No `-ms-` or legacy flexbox prefixes are emitted.
- Seed project CSS size reduced by ≥15% vs. unminified build output.
- All new tests pass in publish workflow.

## Deliverables
- Tokenizer and serializer under `Engine/Pipelines/Css/*` with unit coverage.
- Updated `CssBundler` to use tokenizer/serializer in publish.
- Slimmed prefix map focused on modern targets.
- Tests: invariants, fixtures, and existing publish checks extended.
- Updated docs: assessment and this plan.

## Risks & Mitigations
- Parser complexity: keep grammar minimal; focus on publish‑safe transforms.
- Data URI handling: keep as opaque tokens; never transform contents.
- Maintenance of prefix table: keep scope small and modern; revisit only on demand.

## Rough Timeline
- Phase 1: 1–2 days.
- Phase 2: 1–2 weeks.
- Phase 3: 3–5 days.
- Phase 4: optional, time‑boxed.

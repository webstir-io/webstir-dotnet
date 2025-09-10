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

Phase 1 — Harden Current Implementation (fast)
- Make minifier string‑aware: skip inside quoted strings and `url(...)`.
- Preserve license comments: keep `/*! ... */`, remove others.
- Extend zero‑unit: allow `0` for `px|em|rem|%|in|cm|mm|pc|pt|ex|vh|vw|vmin|vmax|ch` when value is zero.
- URL safety: re‑emit `url(...)` with quotes when characters require quoting; do not alter data URIs.
- Trim prefix map to modern defaults:
  - Remove all `-ms-*` prefixes and old flexbox syntaxes (`-webkit-box`, `-ms-flexbox`).
  - Keep only prefixes that remain useful on modern engines (e.g., `-webkit-appearance`, `-webkit-user-select`).
- Tests (publish workflow):
  - Strings unchanged; `url(data:...)` unchanged.
  - `/*! ... */` kept; other comments removed.
  - `calc()` spacing remains valid; zero units normalized.
  - No `-ms-` prefixes or legacy flexbox artifacts in output.

## TODO

- [x] Make minifier string‑aware (skip strings and `url(...)`).
- [x] Preserve `/*! ... */` license comments.
- [x] Harden `url(...)` quoting and leave data URIs untouched.
- [x] Extend zero‑unit handling to rem/vh/vw/vmin/vmax/ch.
- [x] Trim prefix map to modern‑only (remove `-ms-*` and old flexbox; keep minimal `-webkit-*`).
- [x] Add publish tests for strings, URLs, and zero units.
- [x] Add publish test for `/*! ... */` license comment preservation.
- [x] Add publish test for `calc()` spacing invariants.
- [x] Add publish test for modern-only prefixes (no `-ms-*`, no old flexbox).

## TODO — Phase 2 (Tokenizer + Serializer)

- [x] Define token model: `enum CssTokenType`, `record CssToken(type, value, start, end)`.
- [x] Implement tokenizer: identifiers/at-keywords, strings (escapes), url(), numbers/dimensions/percentages, hash, delimiters, braces/parens/brackets, whitespace, comments.
- [x] Treat `/*! ... */` as preserved comment tokens; drop others.
- [x] Unit tests: tokenizer and serializer invariants (data URIs, escapes).
- [x] Implement serializer: minimal whitespace insertion; remove trailing `;` in blocks; preserve strings/urls verbatim; keep `/*! ... */`.
- [x] Move minification passes to token level: whitespace collapse, number/zero-unit normalization, hex color shortening.
- [x] Ensure `calc()` contents remain valid (spaces optional on modern engines).
- [x] Integrate: replace `Transformer.Minify` internals with tokenizer+serializer while keeping the same API.
- [x] Keep current modern-only prefixing order: prefix, then minify.
- [x] Tests: keep Phase 1 invariants green; add serializer/minifier unit tests.
- [ ] Add golden snapshot test for seed CSS output.


Phase 2 — Tokenizer + Serializer (safer core)
- Implement a CSS tokenizer covering: strings (escapes), URLs, comments, numbers, dimensions, percentages, delimiters, braces/parens/brackets, identifiers/at‑keywords, whitespace.
- Serializer emits minimal, correct whitespace; removes final `;` in blocks.
- Move current minification rules to operate on tokens instead of regex.
- Keep hex color shortener and numeric normalization token‑aware.
- Tests: round‑trip tokenization, serializer invariants, golden snapshots on seed app.

Phase 3 — Minimal AST + Prefixer
- AST: Stylesheet → (AtRule | QualifiedRule) → Declarations.
- Prefixer: property/value‑level only, with a small, static, modern‑focused table.
  - Examples: `-webkit-appearance`, `-webkit-user-select`.
  - Do not add legacy flexbox or `-ms-` variants.
- Tests: fixtures asserting correct insertion order (prefixed before unprefixed), and no duplicates.

Phase 4 — Safe Enhancements (optional)
- Shorthand zero collapsing (e.g., `margin: 0 0 0 0` → `0`).
- Function spacing micro‑minification (`calc()`), token‑aware only.
- Color canonicalization where unambiguous.

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

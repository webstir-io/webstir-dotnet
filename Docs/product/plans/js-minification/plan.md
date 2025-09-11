# JS Minification — Plan

Modern defaults. No external tools. No configs. Publish-only.

## Goals
- Safe, built-in JS minification for publish.
- ESM-only pipeline; keep output deterministic.
- Preserve strings, templates, and regex literals.

## Non‑Goals
- Identifier mangling or aggressive control‑flow transforms.
- Supporting CommonJS bundling.
- Source maps in publish.

## Defaults (Modern Targets)
- Assume evergreen browsers (latest Chrome, Firefox, Safari, Edge).
- Favor safety over marginal byte savings.

## Approach

### Phase 1 — Harden Current Implementation (fast)
Objective: Make minification safe without changing bundler shape.
Status: Planned

#### TODO

- [] Preserve license comments: keep only `/*! ... */`, drop others (do not keep `//!`).
- [] Safe stripper (no full AST): implement a mode-based scan to skip strings, template literals, and regex literals while removing comments.
  - Modes: code, single/double‑quoted string, template literal (including `${...}` passthrough), block comment, line comment, regex literal.
  - Regex literal detection: treat `/` as a regex when the previous significant token cannot end an expression (start of file or one of: `(`, `{`, `[`, `,`, `;`, `:`, `?`, operators, `=>`, keywords like `return|throw|case|of|delete|typeof|void|new|in|instanceof`).
  - Regex body handling: support escapes, character classes `[ ... ]`, and trailing flags.
- [] Whitespace rules (conservative): collapse only when unambiguous.
  - Keep newlines after `return|throw|break|continue|yield` unless a `;` is emitted.
  - Insert a single space if removal would merge tokens (identifier/identifier, identifier/number, number/identifier) or create `++/--` adjacency pitfalls.
  - Do not alter contents of strings, template literals, or regex bodies.
- [] Publish tests: string/template/regex preservation; license handling; no `//# sourceMappingURL` or `/*# sourceMappingURL=... */` in app files.

### Phase 2 — Tokenizer + Serializer (safer core)
Objective: Move minification to a token pass and controlled serializer.
Status: Planned

#### TODO

- [ ] Extend tokenizer: detect regex literals; surface simple keywords and punctuators for spacing decisions.
- [ ] Token categories: value, punctuator, operator, keyword, trivia; decide regex vs. division using previous token category.
- [ ] Serializer: minimal spaces and newlines; emit semicolons where required; preserve `?.`, `??`, bigint (`123n`), numeric separators, private fields (`#x`), and `import.meta` as-is.
- [ ] Do not alter the bytes of strings, template literals, or regex bodies.
- [ ] Migrate `JsTransformer.Minify` to operate on tokens; keep the public API.
- [ ] Tests: ASI edge cases, operator adjacency, regex boundaries, template interpolation.

### Phase 3 — Transport Compression (publish)
Objective: Generate precompressed JS at publish using .NET built-ins.
Status: Planned

#### TODO

- [ ] Publish step: create `.js.br` (Brotli) and `.js.gz` (gzip) alongside originals.
- [ ] Implementation: use `BrotliStream` (quality 11) and `GZipStream` (level 6–9); no external tools.
- [ ] Scope: compress `.js` only; skip already‑compressed binaries (images, fonts, videos).
- [ ] Determinism: stable outputs for identical inputs; avoid per‑request/on‑the‑fly compression.
- [ ] Docs: serving guidance (`Content-Encoding`, `Vary: Accept-Encoding`); CDN cache key notes.
- [ ] Tests: publish fixtures include `.br/.gz` for JS; verify presence and that sizes are smaller than originals; confirm binaries are not precompressed.

### Phase 4 — Safe Enhancements (optional)
Objective: Add small, safe reductions post-tokenization.
Status: Planned

#### TODO

- [ ] Merge adjacent `const`/`let` declarations (already prototyped; move to token stage).
- [ ] Remove `debugger;` and strip `console.*` in publish (consider behind a flag later if needed).
- [ ] Deduplicate trivial wrapper boilerplate in concatenation if introduced.

### Phase 5 — Tree Shaking Tightening (optional)
Objective: Improve usage analysis without a full AST.
Status: Planned

#### TODO

- [ ] Follow re-export chains and namespace imports when computing used exports.
- [ ] Guard against false positives; keep analysis conservative.
- [ ] Tests: entry‑point usage, namespace import access patterns.

## Acceptance Criteria
- Dist JS has no comments except `/*! ... */` when present.
- Strings, template literals, and regex bodies are preserved byte‑for‑byte.
- No `//# sourceMappingURL` lines or `/*# ... */` blocks in dist JS.
- No CommonJS in bundles; diagnostics recorded if encountered.
- Seed project JS size reduced by ≥15% vs. unminified output.
- All new publish/unit tests pass.
 - Seed app still loads and renders in publish (smoke behavior unchanged).
 - Publish outputs precompressed `.js.br` and `.js.gz` for JS; binaries are not double‑compressed.

## Deliverables
- Updated `JsTransformer.Minify` with token‑aware logic.
- Optional tokenizer extensions under `Engine/Pipelines/Core/Parsing/*`.
- Publish compression utility using .NET `BrotliStream`/`GZipStream` and wiring in the publish pipeline.
- Tests: invariants and publish fixtures covering edge cases.
- Tests verifying presence of `.br/.gz` artifacts for JS and skipping binaries.
- Updated docs: assessment and this plan.

## Risks & Mitigations
- Regex literal detection is tricky: start with conservative heuristics; prefer preserving whitespace over breaking code.
- ASI spacing pitfalls: encode explicit serializer rules; back with tests.
- Maintenance of custom minifier: keep scope small; provide an opt‑in path to external tools if future needs grow.
 - Template literals: treat contents as opaque; do not rewrite inside `${...}` in Phase 1; revisit in Phase 2 only if safe.

## Transport Compression (gzip/Brotli)
- Purpose: shrink transfer size; independent of minification output.
- Default: precompress static `.js` at publish as `.js.br` (Brotli) and `.js.gz` (gzip).
- Serving:
  - Prefer `br` for modern browsers; fall back to `gzip`.
  - Set headers: `Content-Encoding` on precompressed responses and `Vary: Accept-Encoding`.
  - CDN: serve precompressed variants; ensure cache keys vary by encoding. Avoid on‑the‑fly compression for cached static assets.
- Levels: use Brotli level 11 for precompressed artifacts; gzip level 6–9.
 - Scope: compress `.js` only. Skip already‑compressed binaries (images, fonts, videos).
- Notes: this is orthogonal to minification; keep behavior deterministic by producing artifacts at publish rather than relying on ad‑hoc edge settings.

## Rough Timeline
- Phase 1: 1–2 days.
- Phase 2: 1–2 weeks.
- Phase 3: ~1 day.
- Phase 4–5: optional, time‑boxed.

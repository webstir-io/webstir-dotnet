# JS Minification — Assessment

## Overview
- Scope: Production publish step only; dev builds do not minify.
- Entry: `Engine/Pipelines/JavaScript/JsHandler.cs:28`.
- Order: TypeScript build → bundle ESM modules → simple transforms (hoist/shake) → minify → write `dist/frontend/pages/<page>/index.<ts>.js` → copy app files (strip source map refs).

## What It Does Today
- Build: Invokes `tsc --build` against base config (`Engine/Pipelines/JavaScript/Build/JsBuilder.cs:42`).
- Module graph + bundling: Builds a graph and concatenates modules (`Engine/Pipelines/JavaScript/Publish/JsBundler.cs:102`, `Engine/Pipelines/JavaScript/Publish/JsBundler.cs:75`). CommonJS is flagged as an error in diagnostics (`Engine/Pipelines/JavaScript/Publish/JsBundler.cs:111`).
- Parsing: Uses a tokenizer-based parser for imports/exports with a regex fallback (`Engine/Pipelines/JavaScript/Parsing/JavaScriptParser.cs:12`, `Engine/Pipelines/JavaScript/Publish/JsModuleParser.cs:26`).
- Transforms:
  - Removes import/export syntax, injects bindings, optional scope hoisting and simple declaration merging (`Engine/Pipelines/JavaScript/Publish/JsModuleTransformer.cs`).
  - Scope hoisting utilities (`Engine/Pipelines/JavaScript/Publish/JsScopeHoister.cs`).
  - Basic tree-shaking by tracking imported names across the graph (`Engine/Pipelines/JavaScript/Publish/JsTreeShaker.cs`).
- Minification: Safe removal of comments/whitespace with literal preservation (`Engine/Pipelines/JavaScript/Publish/JsMinifier.cs`). Applied before writing dist (`Engine/Pipelines/JavaScript/Publish/JsBundler.cs`). No mangling.
- Source maps: Generator exists but publish strips `sourceMappingURL` comments and does not ship maps (`Engine/Pipelines/JavaScript/Publish/JsPublisher.cs:33`, `Engine/Pipelines/JavaScript/Publish/JsPublisher.cs:36`).

Key implementation files:
- `Engine/Pipelines/JavaScript/JsHandler.cs:31`
- `Engine/Pipelines/JavaScript/Publish/JsBundler.cs:102`
- `Engine/Pipelines/JavaScript/Publish/JsMinifier.cs`
- `Engine/Pipelines/JavaScript/Parsing/JavaScriptParser.cs:12`
- `Engine/Pipelines/JavaScript/JsRegex.cs:5`
- Test: `Tests/Workflows/Publish/JsIsMinified.cs:29`

## Strengths
- Zero-dependency bundler/minifier; predictable and fast.
- Enforces modern ESM; flags CommonJS.
- Basic hoisting and usage-based tree-shaking reduce some dead code.

## Gaps and Risks
- Regex minifier is not token-aware: risks stripping comment markers inside strings/templates/regex literals (e.g., `http://`, `/* ... */` in strings, regex bodies).
- Whitespace collapse is naive: can run into ASI hazards (e.g., `return` followed by newline/regex), `++/--` edge cases, or operator adjacency issues.
- No identifier mangling or expression-level compression; size wins are limited.
- Tree-shaking is shallow (import-surface only) and does not analyze intra-module usage.
- Source maps are not emitted for publish (by design), so debugging minified output is harder.

## Recommendations

Short-term (safe, incremental)
- Replace regex-only minifier with a token-aware pass: skip strings, template literals, and regex literals; strip comments safely; collapse whitespace only between tokens where unambiguous.
- Preserve `/*! ... */` license blocks; drop all other comments. Adjust tests to allow licenses.
- Keep stripping `//# sourceMappingURL` lines and `/*# sourceMappingURL=...*/` blocks in publish app files.
- Add publish tests: preserve string/template/regex contents; ensure no inline `//` removal in strings; guard common ASI cases.

Medium-term (better output, optional deps)
- Extend tokenizer to detect regex literals and simple keywords relevant to ASI rules; add a serializer that controls minimal spaces/semicolons.
- Optional: provide a pluggable path to Terser/SWC/esbuild for teams that want deeper minification, gated behind an opt-in.

Non-goals (for now)
- Aggressive compression/mangling that changes identifiers or rewrites control flow.
- Supporting CommonJS bundling.
- JS source maps in publish.

## Acceptance Criteria
- Publish JS has no comments except `/*! ... */` when present in sources.
- String literals, template literals, and regex bodies are byte-preserved.
- No `//# sourceMappingURL` lines or `/*# ... */` blocks in dist JS.
- ESM-only: no `require()`/`module.exports` in bundled modules; diagnostics raised if present.
- ≥15% size reduction on the seed project JS vs. unminified bundle.
- All publish/unit tests green.

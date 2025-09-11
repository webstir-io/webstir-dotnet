# CSS Minification — Assessment

## Overview
- Scope: Production publish step only; dev builds do not minify.
- Entry: `Engine/Pipelines/Css/Publish/CssBundler.cs:BuildBundledCssAsync`.
- Order: bundles modules → resolves URLs → wraps `@media` imports → adds vendor prefixes → minifies → writes `dist/frontend/pages/<page>/index.<ts>.css` and updates per‑page manifest.

## What It Does Today
- Tokenization/serialization: token-aware minifier that preserves string/url contents and `/*! ... */` license comments.
- Whitespace/punctuation: collapses runs, removes trailing `;` before `}`, trims output; ensures spaces around `and`/`or`/`not` in `@media/@supports`.
- Colors: compresses `#RRGGBB` → `#RGB` and `#RRGGBBAA` → `#RGBA` when reducible; canonicalizes select named colors when strictly shorter (`white`→`#fff`, `black`→`#000`, `fuchsia`→`#f0f`, `yellow`→`#ff0`, `transparent`→`#0000`).
- Numbers: drops leading zero in decimals (`0.5` → `.5`), trims trailing `.0` (`1.0` → `1`).
- Zero units: normalizes zero dimensions/percentages to `0`; collapses zero shorthands for `margin`/`padding`/`inset` to a single `0` when safe.
- Prefixing: minimal modern-only injection (e.g., `-webkit-user-select`, `-webkit-appearance`), with duplicate guards per block; excludes legacy flexbox and `-ms-*`.

Key implementation files:
- `Engine/Pipelines/Css/Publish/CssBundler.cs:49`
- `Engine/Pipelines/Css/Publish/CssBundler.cs:63`
- `Engine/Pipelines/Css/Publish/CssTransformer.cs:86`
- `Engine/Pipelines/Css/CssRegex.cs:5`
- Test: `Tests/Workflows/Publish/CssIsMinified.cs:29`

## Strengths
- Fast, zero‑dependency implementation.
- Handles common, safe reductions (comments, trivial whitespace, final semicolons, hex/numeric compaction).
- Basic autoprefixing covers frequent flexbox cases.

## Gaps and Risks
- Prefix coverage: limited static map; no Browserslist targeting; no grid/backdrop‑filter, etc.
- Canonicalization scope: limited to trivial hex/name cases; no function-level color transforms (`rgb()`/`hsl()`), no selector/declaration merging.
- Structural awareness: no AST; transforms avoid reordering/merging to protect cascade/specificity.
- Source maps: generator exists but publish intentionally does not emit CSS maps.
- URL quoting: `UpdateUrls` adds quotes only when necessary for validity; complex edge cases may surface.

## Recommendations

Short‑term (safe, incremental)
- Keep prefix table minimal and modern; consider opt-in removal of legacy prefixes in publish.
- Add more value-safe canonicalizations only when strictly shorter and unambiguous.
- Expand zero-shorthand support cautiously (e.g., `inset-inline`, `inset-block`) with tests.
- Continue to harden URL quoting rules via publish fixtures.

Medium‑term (better output, optional deps)
- Autoprefixer: integrate Node `autoprefixer` with Browserslist for up‑to‑date prefixes.
- AST: introduce a minimal AST only if future transforms require structural rewrites.
- Configurability: expose minimal toggles (e.g., preserve licenses) in a repo config.

Non‑goals (for now)
- Aggressive rule merging/reordering: keep pipeline simple and predictable.
- CSS source maps in publish: current docs intentionally exclude them.

## Acceptance Criteria
- Publish CSS has no comments except `/*! ... */`.
- Strings and `url(...)` contents are preserved for validity.
- Only modern prefixes; no legacy `-ms-` or old flexbox.
- ≥15% size reduction on the seed project vs. unminified.
- All publish/unit tests green.

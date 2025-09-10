# CSS Minification — Assessment

## Overview
- Scope: Production publish step only; dev builds do not minify.
- Entry: `Engine/Pipelines/Css/Publish/CssBundler.cs:BuildBundledCssAsync`.
- Order: bundles modules → resolves URLs → wraps `@media` imports → adds vendor prefixes → minifies → writes `dist/frontend/pages/<page>/index.<ts>.css` and updates per‑page manifest.

## What It Does Today
- Comment removal: strips all block comments `/* ... */`.
- Whitespace: collapses runs of whitespace to a single space; removes spaces around `{}:;,>+~`; trims final output.
- Punctuation: removes spaces after `:` and the last `;` before `}`.
- Colors: compresses `#RRGGBB` to `#RGB` when safe.
- Numbers: drops leading zero in decimals (`0.5` → `.5`) and trailing `.0` (`1.0` → `1`).
- Zero units: replaces `0px|em|%|in|cm|mm|pc|pt|ex` with `0`.
- Prefixing: injects a small, hard‑coded set of vendor prefixes (flexbox, transform/transition/animation, user‑select, etc.).

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
- Strings: regexes operate inside quoted strings and `url(...)`, potentially altering content (e.g., `content: " : ; {} ";`, inline SVG/data URIs).
- Comments: no preservation of important comments `/*! ... */` (licenses, banners).
- Prefix coverage: limited static map; no Browserslist targeting; no grid/backdrop‑filter, etc.
- URL quoting: `UpdateUrls` emits `url(<value>)` without quotes; values with special characters may need quoting.
- Rule semantics: no merging/deduplication; no awareness of cascade or specificity.
- Source maps: a generator exists but publish does not emit CSS maps by design.
- Tests: only check “no comments / collapsed whitespace”; no safety checks for strings/URLs/functions.

## Recommendations

Short‑term (safe, incremental)
- Make minifier string‑aware: skip whitespace/operator rewrites inside quotes and within `url(...)`.
- Preserve important comments: keep `/*! ... */` blocks; drop others.
- Extend zero‑unit handling carefully (e.g., `rem|vh|vw|vmin|vmax|ch`) after adding tests; all are safe for `0`.
- Harden URL handling: re‑emit as `url("...")` when value contains characters that require quoting.
- Add publish tests that protect invariants:
  - Quoted strings remain byte‑for‑byte identical.
  - `url(data:...)` values remain valid and unmodified.
  - `calc()` spacing reductions keep valid syntax.
  - `0%` → `0` is accepted by engines we target.

Medium‑term (better output, optional deps)
- Autoprefixer: integrate Node `autoprefixer` with Browserslist in the publish pipeline for accurate, up‑to‑date prefixes.
- Minifier: adopt a robust minifier with string/AST awareness (e.g., Node `cssnano`). If avoiding Node, consider a .NET library with safe transforms.
- Configurability: expose a minimal config (e.g., preserve licenses, target browsers) in `webstir.config.*`.

Non‑goals (for now)
- Aggressive rule merging/reordering: keep pipeline simple and predictable.
- CSS source maps in publish: current docs intentionally exclude them.

## Acceptance Criteria
- Publish CSS has no comments (except `/*! ... */` when configured to preserve).
- No changes to string literals or `url(...)` contents.
- Size reduction is measurable on seed project (>15% vs. unminified).
- Tests pass for the added invariants above.


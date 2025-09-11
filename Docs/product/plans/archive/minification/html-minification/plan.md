# HTML Minification Plan

## Overview
Opinionated, always-on HTML minification in the publish workflow. Keep it safe and simple; no flags or configuration.

## Milestone A — Streaming Minifier (Completed)
- Implemented `HtmlMinifier` (single-pass, streaming):
  - Removes HTML comments outside raw/sensitive blocks
  - Collapses inter-tag whitespace (does not alter text nodes)
  - Preserves `<script>`, `<style>`, `<pre>`, `<code>`, `<textarea>` contents
- Attribute optimizations (safe-only):
  - Collapse boolean attributes (e.g., `disabled="disabled"` → `disabled`)
  - Remove default `type` on `<script>` (`text/javascript`) and `<style>` (`text/css`)
  - Safely unquote attribute values when unambiguous (no spaces/quotes/`<`/`>`/`=`)
  - Do not reorder attributes; do not lowercase names; do not remove empty attributes
- Element optimizations:
  - Do not remove optional opening/closing tags in v1

Note: We skipped building a standalone tokenizer/serializer for v1 to ship faster with lower risk.

## Milestone B — Pipeline Integration (Completed)
- `HtmlBundler` now calls `HtmlMinifier.Minify(...)`
- Asset manifest rewriting remains before minification
- Dist HTML gets `.html.br` and `.html.gz` variants
- Graceful fallback on errors (emit original HTML, log warning)

## Milestone C — Tests (Completed)
- Workflow tests only (no unit tests):
  - Inter-tag whitespace collapse
  - Comments removed and attribute optimizations
  - Precompressed artifacts exist for HTML

## Milestone D — Follow-ups (Planned)
- Diagnostics: include file/line context for minifier warnings
- Architecture (optional): consider tokenizer/serializer refactor if future needs arise
- Enhancements (optional): evaluate minifying inline `<script>`/`<style>` using existing JS/CSS minifiers

## Success Metrics
- Size: 10–20% reduction for typical pages
- Performance: <100ms added per page
- Correctness: No visual/functional regressions; all existing tests pass

## Rollout
- Always on. No flags/config. Revisit only if real-world issues require changes.

## Decisions
- Preserve `data-*` and unknown attributes as-is
- Do not lowercase attribute names (SVG/MathML safety)
- Do not remove optional tags in v1
- No browser-based visual tests; rely on workflow tests

## Next Steps
1. Add diagnostics with file/line context
2. Reassess need for tokenizer/serializer or inline minification in a future iteration

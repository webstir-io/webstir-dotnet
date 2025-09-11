# HTML Minification Assessment (Post‑Implementation)

## Overview
HTML minification is implemented and enabled by default in the publish workflow. The implementation uses a streaming, single‑pass minifier focused on safe, deterministic transforms.

## Current State
- HTML assembled from `app.html` + page fragments during build.
- On publish, HTML is rewritten to use per‑page manifests, minified, and precompressed as `.html.br` and `.html.gz`.

## Approach
- Streaming minifier (`HtmlMinifier`): parses tags/text in one pass without a standalone tokenizer/serializer.
- Opinionated defaults; no flags/configuration.

### Optimizations Applied
- Remove HTML comments (outside raw/sensitive blocks).
- Collapse inter‑tag whitespace (whitespace‑only text nodes between tags).
- Attribute optimizations (safe‑only):
  - Collapse boolean attributes.
  - Remove default `type` on `<script>` (`text/javascript`) and `<style>` (`text/css`).
  - Safely unquote attribute values when unambiguous (no spaces/quotes/`<`/`>`/`=`).

### Preserved Semantics
- Do not alter contents of `<script>`, `<style>`, `<pre>`, `<code>`, `<textarea>`.
- Do not reorder or lowercase attributes; preserve unknown/data attributes as‑is.
- Do not remove optional opening/closing tags in v1.

## Integration
- Runs in `HtmlBundler` after asset reference rewriting.
- Emits precompressed `.html.br` and `.html.gz` variants next to `index.html`.
- Graceful fallback on errors (keep original HTML, log warning).

## Risks and Mitigations
- Risk: whitespace‑sensitive layouts → Mitigation: only collapse inter‑tag whitespace; preserve text nodes and sensitive elements.
- Risk: attribute semantics → Mitigation: safe‑only transforms; no reordering/lowercasing; preserve unknown/data attrs.
- Risk: inline JS/CSS → Mitigation: raw handling for `<script>/<style>`; no inline minification.

## Success Criteria
- All existing tests pass with minification enabled (done).
- No visual or functional regressions observed in workflow tests (done).
- Clear warning on minifier failure with graceful fallback (done).

## Testing Strategy
- Workflow tests only (no unit tests): whitespace collapse, attribute optimizations, precompressed artifact presence.

## Future Considerations (Optional)
- Add file/line context to diagnostics where practical.
- Consider tokenizer/serializer refactor if future rules get more advanced.
- Evaluate opt‑in minification for inline `<script>/<style>` using existing JS/CSS minifiers.

# CSS Pipeline Updates

## Goal
Use the tokenizer for `@import` extraction while preserving current minification, URL rewriting, and CSS Modules behavior.

## Flow
1. Tokenize CSS files sufficiently to detect `@import` and optional media tail.
2. Build module graph for CSS; order via topological sort.
3. Process modules:
   - Remove `@import` lines after resolution.
   - Apply existing CSS Modules processing where `.module.css` is detected.
   - URL rewriting and media query wrapping kept as-is.
4. Concatenate and minify.

## Validation Checklist
- `@import url(...)` and `@import '...'` both supported; media captured accurately.
- Comments and whitespace do not break detection.
- Existing class mapping and URL rewriting remain unchanged in output.


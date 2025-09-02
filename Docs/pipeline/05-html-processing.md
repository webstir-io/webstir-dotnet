# HTML Processing

## Goal
Keep HTML as first-class with simple build/publish phases and diagnostics integration.

## Build
- Merge page fragments into app template (current behavior).
- Ensure diagnostics for missing base files (`app.html`) and malformed fragments.

## Publish
- Strip dev-only artifacts (refresh scripts/comments) and minify.
- Keep structure untouched beyond whitespace (
  favor safety over aggressive rewrites).

## Validation Checklist
- Build surfaces clear errors when template is missing.
- Publish output is semantically identical with reduced whitespace.


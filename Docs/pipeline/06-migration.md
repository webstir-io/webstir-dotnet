# Migration

Concise guidance for adopting the new pipelines. No rollout/backout plan is required — changes are already contained within `Engine/Pipelines/*` and use fail‑fast diagnostics with clear remediation.

## Defaults
- Tokenizer enabled for JS import/export and CSS `@import` extraction.
- TypeScript required; fail fast with clear diagnostics if missing.
- No runtime feature flags; ship when validation is green.

## What To Do
- Use handlers/builders in `Engine/Pipelines/*`; no changes outside this tree.
- Ensure `tsc` is available on PATH; compilation runs before JS bundling.
- Keep existing transform/minification behavior; tokenizer is for dependency extraction only.

## Compatibility
- ES modules only in v1. CommonJS is not supported initially.
- Detect common CJS patterns and emit clear diagnostics with links to docs:
  - `require('x')`, `module.exports =`, `exports.foo =` → suggest `import`/`export` equivalents.
- Provide short guidance for migrating to ESM (examples below).

### CommonJS → ESM quick mapping
- `const x = require('x')` → `import x from 'x'`
- `const {a:b} = require('x')` → `import { a as b } from 'x'`
- `module.exports = foo` → `export default foo`
- `exports.bar = bar` → `export { bar }`

## Validation Checklist
- Diagnostics: counts and first N errors summarized clearly in CLI output.
- CSS: `@import` forms supported (`string` and `url(...)`), media captured; comments/whitespace tolerated.
- JS: static imports, re‑exports, and `import('...')` with string literal detected; `import type` ignored.
- TS: `tsc` runs using repo `base.tsconfig.json` and project references; outputs to build.
- Source maps: JS bundle emits matching `.map` files.
- Paths: behavior correct on Windows/macOS/Linux (path separators normalized).
- Performance: no material regressions on sample projects (bundle time within ±10%).

## Test Matrix
- Mixed JS/TS project builds and runs.
- CSS with nested `@import`s, media queries, and comments.
- JS with default/named/namespace imports, re‑exports, and one dynamic import.
- Large file with long comments and strings to exercise tokenizer recovery.

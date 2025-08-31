# Overview

## Vision
A batteries-included web framework that treats HTML, CSS, and JavaScript as first-class. The pipeline is simple, fast, and resides entirely under `Engine/Pipelines`. No separate `Engine/Shared` layer.

## Scope and Constraints
- All core primitives live under `Engine/Pipelines/Core`.
- TypeScript is required (tooling dependency) for the JavaScript pipeline.
- Initial tokenizer scope is minimal and targeted:
  - JavaScript: imports/exports (static + basic re-exports), dynamic import detection.
  - CSS: `@import` with optional media queries and `url()`.
- CommonJS: not supported in v1 of this plan; revisit after ES module parity and stability.
 - Framework-agnostic: no React/Next.js coupling; no JSX in v1.

## Why this shape
- Keep ownership and iteration velocity by avoiding a separate shared layer.
- Constrain complexity: specialize tokenizer to the dependency graph needs, not a general JS parser.
- Improve diagnostics early to raise developer confidence.

## Outcomes
- More robust dependency extraction for JS/CSS.
- Better error messages (file/line/column) from Core.
- Clear sequencing for Build vs. Publish across HTML/CSS/JS.

## Non-Goals (v1)
- JSX transformation or React runtime support.
- Next.js-style routing/SSR or framework conventions.
- CommonJS interop.

## Success Criteria
- Builds pass with TS required and installed.
- Tokenizer correctly identifies imports/exports and CSS `@import` in real projects.
- Diagnostics surface precise locations for parsing/tokenization failures.
- Minimal churn to existing bundlers during transition.

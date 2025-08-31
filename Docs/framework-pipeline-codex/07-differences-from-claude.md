# Differences vs. Claude’s Draft

## Structural
- No `Engine/Shared/*` namespace. Everything lives under `Engine/Pipelines/*`.
- Core primitives consolidated in `Engine/Pipelines/Core` and referenced directly by CSS/JS/HTML.

## TypeScript
- TypeScript is required for the JS pipeline (not optional). The CLI should fail fast with a clear diagnostic if `tsc` is unavailable.

## Tokenizer Scope
- Narrow focus: import/export in JS and `@import` in CSS.
- Not a full parser; avoids AST complexity while eliminating regex brittleness for dependency graph.

## CommonJS
- Explicitly out-of-scope for v1 of this plan. Prioritize stable ESM bundling with TS; revisit CJS later with a dedicated design.

## Rollout Philosophy
- Integrate diagnostics first, then swap in tokenizer for extraction only.
- Preserve existing bundlers/transformations initially to minimize churn and regressions.

## Framework-Agnostic Stance
- No React/Next.js assumptions or JSX support in v1.
- Accepted file types: `.ts`, `.js` only for JavaScript pipeline.

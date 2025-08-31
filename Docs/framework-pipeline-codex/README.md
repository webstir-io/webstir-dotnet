# Webstir Framework Pipeline — Codex Plan

This is a focused plan for evolving the pipelines with everything contained under `Engine/Pipelines` (no `Engine/Shared/*`), with TypeScript required, and a narrow tokenizer used only for import/export (`JS`) and `@import` (`CSS`).

Documents:
- 00-overview.md — Vision, scope, constraints
- 01-core-and-diagnostics.md — Core v2 under `Engine/Pipelines/Core`
- 02-tokenizer.md — Minimal tokenizer + parsers under `Pipelines/Core/Parsing`
- 03-javascript-pipeline-ts.md — JS/TS pipeline with `tsc` required
- 04-css-pipeline.md — CSS pipeline updates
- 05-html-processing.md — HTML pipeline updates
- 06-migration.md — Rollout plan (no feature flags for now)
- 07-differences-from-claude.md — Key differences vs. earlier draft
- 08-cache-busting-and-fingerprinting.md — Cache busting strategy and filename fingerprinting
- 09-working-agreements.md — Feature-specific agreements for this pipeline work

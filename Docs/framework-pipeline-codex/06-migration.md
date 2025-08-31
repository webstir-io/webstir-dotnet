# Migration

## Strategy
- Non-destructive, phased rollout contained in `Engine/Pipelines/*`.
- Introduce Core diagnostics first; wire through existing handlers/bundlers.
- Adopt tokenizer for dependency extraction only (JS/CSS), leaving transforms as-is.
- Make TypeScript compilation mandatory; fail fast with actionable diagnostics if missing.

## Defaults (no flags)
- Tokenizer enabled for JS import/export and CSS @import extraction.
- TypeScript required; fail fast with clear diagnostics if missing.

## Compatibility
- ES modules only in v1. If a project relies on CommonJS, document as unsupported initially.
- Provide guidance for migrating common patterns to ESM.

## Validation Checklist
- Diagnostics summarize counts and first N errors clearly in CLI output.

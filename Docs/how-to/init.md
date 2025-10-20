# Init

Create a new project from embedded templates. Produces a ready-to-run layout with frontend, backend, shared code, and types.

## Purpose
- Scaffold a clean workspace with sensible defaults.
- Zero-config start: `watch` runs immediately after init.

## When To Use
- Starting a new app or demo.
- Recreating a minimal workspace for tests or examples.

## CLI
- `webstir init [--client-only|--server-only] [--project-name|-p <name>] [directory]`

## Inputs & Flags
- Target directory (optional; defaults to current working directory).
- `--client-only` or `--server-only` to limit templates.
- `--project-name|-p` to set the app name in template files.

## Steps
1. Validate or create the target directory.
2. Copy templates: frontend, backend, shared, types.
3. Normalize names and placeholders (project name, defaults).
4. Write minimal config files used by the engine and templates (including `webstir.providers.json`).

## Outputs
- Source tree under `src/**` and `types/`.
- Base HTML at `src/frontend/app/app.html` (must contain `<main>`).
- Backend entry at `src/backend/index.ts`.
- Shared modules under `src/shared/**`.
- Provider map at `webstir.providers.json` (defaults to Webstir-managed providers; edit to swap providers).

## Errors & Exit Codes
- Non-zero on invalid directory, name normalization failure, or IO errors.
- Logs describe which file or step failed.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- CLI — [cli](../reference/cli.md)
- Engine — [engine](../explanations/engine.md)
- Workspace — [workspace](../explanations/workspace.md)
- Pipelines — [pipelines](../explanations/pipelines.md)
- Tests — [tests](../explanations/testing.md)

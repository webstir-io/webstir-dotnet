# Init

Create a new project from embedded templates. Produces a ready‑to‑run layout with client, server, shared code, and types.

## Purpose
- Scaffold a clean workspace with sensible defaults.
- Zero‑config start: `watch` runs immediately after init.

## When To Use
- Starting a new app or demo.
- Re‑creating a minimal workspace for tests or examples.

## CLI
- `webstir init [--client-only|--server-only] [--project-name|-p <name>] [directory]`

## Inputs & Flags
- Target directory (optional; defaults to current working directory).
- `--client-only` or `--server-only` to limit templates.
- `--project-name|-p` to set the app name in template files.

## Steps
1. Validate or create the target directory.
2. Copy templates: client, server, shared, types.
3. Normalize names and placeholders (project name, defaults).
4. Write minimal config files used by the engine and templates.

## Outputs
- Source tree under `src/**` and `types/`.
- Base HTML at `src/client/app/app.html` (must contain `<main>`).
- Server entry at `src/server/index.ts`.
- Shared modules under `src/shared/**`.

## Errors & Exit Codes
- Non‑zero on invalid directory, name normalization failure, or IO errors.
- Logs describe which file or step failed.

## Related Docs
- Workflows — [workflows](../workflows.md)
- CLI — [cli](../../solution/cli/cli.md)
- Engine — [engine](../../solution/engine/engine.md)
- Workspace — [workspace](../../solution/workspace/workspace.md)
- Pipelines — [pipelines](../../pipelines/pipelines.md)
- Tests — [tests](../../solution/tests/tests.md)

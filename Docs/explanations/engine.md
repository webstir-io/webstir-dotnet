# Engine

Core implementation that powers the CLI. The engine owns workflows, pipelines, servers, watching, and the project workspace. This doc expands the high-level overview in [solution.md](solution.md).

## Overview
- Orchestrates end-to-end workflows: init → build → watch/test → publish.
- Implements HTML/CSS/TS and static asset pipelines (Images, Fonts, Media), plus dev servers.
- Uses a strongly typed workspace (`AppWorkspace`) for all paths.
- Keeps defaults simple; favors conventions over configuration.

## Responsibilities
- Parse and validate input workspace (folders, files, options).
- Run pipelines for client, server, and shared code.
- Serve `build/` in dev; proxy `/api/*` to the Node server.
- Watch `src/**`, perform incremental rebuilds, and reload clients via SSE.
- Optimize and emit production assets in `dist/` with manifests.

## Structure
- Workflows — high-level orchestration: [workflows](../reference/workflows.md)
- Pipelines — HTML, CSS, JS/TS stages: [pipelines](pipelines.md)
- Services — dev, watch, change coordination: [dev service](devservice.md)
- Workers — per-area units (client/server/shared)
- Servers — dev static server (ASP.NET Core) + Node API host
- Templates — embedded project scaffolding: [templates](../reference/templates.md)
- Workspace — paths and constants: [workspace](workspace.md)

## Workflows
- init: Create a project from templates (client, server, shared, types). Writes a ready-to-run structure.
- build: Compile TS, process CSS, merge page HTML with `app.html`, emit to `build/`.
- watch: Run build and tests, start servers, then react to changes with targeted rebuilds and reloads.
- test: Build, execute compiled tests in Node, return CI-friendly exit codes.
- publish: Optimize and fingerprint assets into `dist/` and rewrite HTML with per-page manifests.
- generators: `add-page`, `add-test` create files in the right locations.

Workflows call workers and services; they should not contain low-level file logic.

## Workers
- FrontendWorker: Runs HTML/CSS/TS frontend pipelines, copies app assets, writes to `build/frontend/`.
- BackendWorker: Compiles backend TS → `build/backend/`, tracks restart state.
- SharedWorker: Ensures shared types are compiled and available to both sides.

Workers are incremental where possible: only touched pages or modules are reprocessed.

## Pipelines
- HTML: Merge page fragments into `src/client/app/app.html` (requires a `<main>`), validate, write to `build/client/pages/<page>/index.html`. In publish, minify and rewrite links from the manifest.
- CSS: Resolve `@import`/URLs, copy assets. In publish, apply autoprefix/minify and optional CSS Modules.
- JS/TS: Use `tsc --build` with an embedded base config. ESM-only graph; tree-shake/minify in publish.
- Assets: Copy Images, Fonts, and Media from `src/client/**` → `build/client/**` (dev) and from `build/**` → `dist/client/**` (publish) with structure preserved.

See details in [pipelines](pipelines.md).

## Services
- DevService: Hosts the dev web server that serves `build/client/**`, exposes an SSE endpoint for reloads, and proxies `/api/*` to the Node server.
- WatchService: Manages file watching over `src/**`, batching events to avoid thrash.
- ChangeService: Classifies changes (client/server/shared), deduplicates, and schedules the minimal work.

See [dev service](devservice.md) for behavior and lifecycle.

## Servers
- Web server: ASP.NET Core minimal app serving `build/client/**`, clean URLs, dev cache headers, SSE for reload.
- Node server: Runs `build/server/index.js`. Restarts on server code changes. Dev server proxies `/api/*` to this process.

## Change Detection
- FileSystemWatcher feeds a buffered queue to coalesce rapid changes.
- Routing rules:
  - Client changes → client pipelines → write to `build/client/**` → broadcast SSE reload.
  - Server changes → server compile → restart Node process.
  - Shared changes → rebuild affected client and server targets.

## Outputs
- Dev: `build/client/**` and `build/server/**`, readable output with source context.
- Prod: `dist/client/pages/<page>/index.<timestamp>.{css|js}`, HTML rewritten to fingerprinted assets, per-page `manifest.json`.

## Errors & Logging
- Use clear, actionable errors; do not swallow exceptions silently.
- Surface build/test failures with non-zero exit codes to the CLI.
- Prefer structured logs that highlight the failing stage and file.

## Extensibility
- No plugin system. Extend by adding pipeline stages or worker capabilities.
- Keep changes local and behavior-preserving; prefer mechanical, minimal diffs.

## Testing
- Favor end-to-end workflow tests over unit tests. See [tests](testing.md) and [.codex/testing.md](../../.codex/testing.md).
- Lock down contracts: commands, flags, exit codes, directory structure, and publish outputs.
- Use snapshot tests for scaffolding and publish artifacts where practical.

## Contracts (Key Invariants)
- Source roots: `src/client/**`, `src/server/**`, `src/shared/**`, `types/**`.
- Dev outputs: `build/client/**`, `build/server/**`.
- Prod outputs: `dist/client/pages/<page>/**` with per-page manifests.
- API proxy: HTTP requests under `/api/*` route to the Node server in dev.
- Base HTML requires `<main>` in `src/client/app/app.html`.

## Related Docs
- Solution overview — [solution](solution.md)
- CLI reference — [cli](../reference/cli.md)
- Pipelines — [pipelines](pipelines.md)
- Dev service — [devservice](devservice.md)
- Workflows — [workflows](../reference/workflows.md)
- Workspace and paths — [workspace](workspace.md)
- Templates — [templates](../reference/templates.md)

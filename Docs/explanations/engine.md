# Engine

Core implementation that powers the CLI. The engine owns workflows, pipelines, servers, watching, and the project workspace. This doc expands the high-level overview in [solution.md](solution.md).

## Overview
- Orchestrates end-to-end workflows: init → build → watch/test → publish.
- Implements HTML/CSS/TS and static asset pipelines (Images, Fonts, Media), plus dev servers.
- Uses a strongly typed workspace (`AppWorkspace`) for all paths.
- Keeps defaults simple; favors conventions over configuration.

## Responsibilities
- Parse and validate input workspace (folders, files, options).
- Run pipelines for frontend, backend, and shared code.
- Serve `build/` in dev; proxy `/api/*` to the Node server.
- Watch `src/**`, perform incremental rebuilds, and reload browsers via SSE.
- Optimize and emit production assets in `dist/` with manifests.

## Structure
- Workflows — high-level orchestration: [workflows](../reference/workflows.md)
- Pipelines — HTML, CSS, JS/TS stages: [pipelines](pipelines.md)
- Services — dev, watch, change coordination: [dev service](devservice.md)
- Workers — per-area units (frontend/backend/shared)
- Servers — dev static server (ASP.NET Core) + Node API host
- Templates — embedded project scaffolding: [templates](../reference/templates.md)
- Workspace — paths and constants: [workspace](workspace.md)

## Workflows
- init: Create a project from templates (frontend, backend, shared, types). Writes a ready-to-run structure.
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

Contracts and DI
- IWorkflowWorker: Common contract used by all workers (`BuildOrder`, `InitAsync`, `BuildAsync`, `PublishAsync`).
- IFrontendWorker: Extends `IWorkflowWorker` with `AddPageAsync`.
- DI registers all workers as `IWorkflowWorker`; workflows inject `IEnumerable<IWorkflowWorker>` and filter by project mode.
- Generators (e.g., add-page) resolve the single `IFrontendWorker` from the injected set and call `AddPageAsync`.

Workers are incremental where possible: only touched pages or modules are reprocessed.

## Pipelines
- HTML: Merge page fragments into `src/frontend/app/app.html` (requires a `<main>`), validate, write to `build/frontend/pages/<page>/index.html`. In publish, minify and rewrite links from the manifest.
- CSS: Resolve `@import`/URLs, copy assets. In publish, apply autoprefix/minify and optional CSS Modules.
- JS/TS: Use `tsc --build` for type checking, then **esbuild** for bundling (10-100x faster). ESM format; tree-shake/minify in publish.
- Assets: Copy Images, Fonts, and Media from `src/frontend/**` → `build/frontend/**` (dev). In publish, optimize (compress images, convert fonts to WOFF2) when tools available, then copy to `dist/frontend/**`.

See details in [pipelines](pipelines.md).

## Services
- DevService: Hosts the dev web server that serves `build/frontend/**`, exposes an SSE endpoint for reloads, and proxies `/api/*` to the Node server.
- WatchService: Manages file watching over `src/**`, batching events to avoid thrash.
- ChangeService: Classifies changes (frontend/backend/shared), deduplicates, and schedules the minimal work.

See [dev service](devservice.md) for behavior and lifecycle.

## Servers
- Web server: ASP.NET Core minimal app serving `build/frontend/**`, clean URLs, dev cache headers, SSE for reload.
- Node server: Runs `build/backend/index.js`. Restarts on backend code changes. Dev server proxies `/api/*` to this process.

## Change Detection
- FileSystemWatcher feeds a buffered queue to coalesce rapid changes.
- Routing rules:
  - Frontend changes → frontend pipelines → write to `build/frontend/**` → broadcast SSE reload.
  - Backend changes → backend compile → restart Node process.
  - Shared changes → rebuild affected frontend and backend targets.

## Outputs
- Dev: `build/frontend/**` and `build/backend/**`, readable output with source context.
- Prod: `dist/frontend/pages/<page>/index.<hash>.{css|js}`, HTML rewritten to fingerprinted assets, per-page `manifest.json`.

## Errors & Logging
- Use clear, actionable errors; do not swallow exceptions silently.
- Surface build/test failures with non-zero exit codes to the CLI.
- Prefer structured logs that highlight the failing stage and file.

## Client Error Reporting
- Client handler: template includes `/app/error.js`, which installs `window.__WEBSTIR_ON_ERROR__` and listens for `error` and `unhandledrejection` events.
- Endpoint: the web server exposes `POST /client-errors`. Payload must be `application/json` and <= 32KB.
- Limits: server returns `415` for non-JSON and `413` for oversized payloads; `204` on success.
- Correlation: handler includes a client-generated correlation id; server also reads `X-Correlation-ID` when present and forwards all reports to `IErrorTrackingService`.
- Guardrails: client deduplicates repeats within 60s and throttles to 1 event/second with a 20-report cap per page session.

## Extensibility
- No plugin system. Extend by adding pipeline stages or worker capabilities.
- Keep changes local and behavior-preserving; prefer mechanical, minimal diffs.

## Testing
- Favor end-to-end workflow tests over unit tests. See [tests](testing.md) and [.codex/testing.md](../../.codex/testing.md).
- Lock down contracts: commands, flags, exit codes, directory structure, and publish outputs.
- Use snapshot tests for scaffolding and publish artifacts where practical.

## Contracts (Key Invariants)
- Source roots: `src/frontend/**`, `src/backend/**`, `src/shared/**`, `types/**`.
- Dev outputs: `build/frontend/**`, `build/backend/**`.
- Prod outputs: `dist/frontend/pages/<page>/**` with per-page manifests.
- API proxy: HTTP requests under `/api/*` route to the Node server in dev.
- Base HTML requires `<main>` in `src/frontend/app/app.html`.

## Related Docs
- Solution overview — [solution](solution.md)
- CLI reference — [cli](../reference/cli.md)
- Pipelines — [pipelines](pipelines.md)
- Dev service — [devservice](devservice.md)
- Workflows — [workflows](../reference/workflows.md)
- Workspace and paths — [workspace](workspace.md)
- Templates — [templates](../reference/templates.md)

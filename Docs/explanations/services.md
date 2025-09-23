# Services

Runtime helpers that coordinate builds, watching, and dev UX. Services keep workflows small by handling long-running behavior and cross-cutting concerns.

## Overview
- DevService: hosts the dev web server, live reload, and API proxy.
- WatchService: watches `src/**`, batches changes, and triggers minimal work.
- ChangeService: classifies changes (frontend/backend/shared) and deduplicates.

See also: [Engine](engine.md) and [Servers](servers.md).

## DevService
- Serves `build/frontend/**` during `watch`.
- Exposes an SSE endpoint to notify connected browsers to reload after frontend rebuilds.
- Proxies `/api/*` to the Node API server that runs `build/backend/index.js`.
- Publishes build status events (`building`, `success`, `error`) that drive the browser badge.
- Applies clean URLs and dev cache headers.

Lifecycle
- Start after initial `build` and `test` in the `watch` workflow.
- On frontend changes: rebuild affected assets → broadcast reload via SSE.
- On backend changes: restart Node process (proxy keeps routing to the new process).

Configuration
- Picks a free port or uses a configured one from CLI options/env.
- Logs the server URL and SSE status on start.

## WatchService
- Watches `src/**` and ignores `build/**` and `dist/**`.
- Buffers and coalesces rapid events to avoid thrashing.
- Emits normalized change events to the ChangeService.

Behavior
- Debounce/queue changes; process in FIFO while merging duplicates.
- Survives editor temp files; only triggers when effective content changes.

## ChangeService
- Categorizes a change as frontend/backend/shared based on path.
- Computes the smallest rebuild unit (page, backend, or both).
- Prevents duplicate or circular work.
- Emits `ClientNotificationType` events so DevService can broadcast status/reload messages.

Routing examples
- `src/frontend/pages/home/index.ts` → frontend rebuild for `home`.
- `src/backend/index.ts` → backend rebuild and restart.
- `src/shared/*.ts` → trigger both sides as needed.

## Responsibilities Split
- Workflows decide “when” to start services and “what” to do next.
- Services decide “how” to watch, proxy, notify, and throttle.

## Errors & Logging
- All services surface clear, actionable logs.
- Failures bubble to the CLI with non-zero exit codes when unrecoverable.

## Related Docs
- Solution overview — [solution](solution.md)
- Engine — [engine](engine.md)
- Servers — [servers](servers.md)
- Pipelines — [pipelines](pipelines.md)
- Workspace — [workspace](workspace.md)
- CLI — [cli](../reference/cli.md)

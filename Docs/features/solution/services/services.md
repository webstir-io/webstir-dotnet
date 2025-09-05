# Services

Runtime helpers that coordinate builds, watching, and dev UX. Services keep workflows small by handling long‑running behavior and cross‑cutting concerns.

## Overview
- DevService: hosts the dev web server, live reload, and API proxy.
- WatchService: watches `src/**`, batches changes, and triggers minimal work.
- ChangeService: classifies changes (client/server/shared) and deduplicates.

See also: [Engine](../engine/engine.md) and [Servers](../servers/servers.md).

## DevService
- Serves `build/client/**` during `watch`.
- Exposes an SSE endpoint to notify connected browsers to reload after client rebuilds.
- Proxies `/api/*` to the Node API server that runs `build/server/index.js`.
- Applies clean URLs and dev cache headers.

Lifecycle
- Start after initial `build` and `test` in the `watch` workflow.
- On client changes: rebuild affected assets → broadcast reload via SSE.
- On server changes: restart Node process (proxy keeps routing to the new process).

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
- Categorizes a change as client/server/shared based on path.
- Computes the smallest rebuild unit (page, server, or both).
- Prevents duplicate or circular work.

Routing examples
- `src/client/pages/home/index.ts` → client rebuild for `home`.
- `src/server/index.ts` → server rebuild and restart.
- `src/shared/*.ts` → trigger both sides as needed.

## Responsibilities Split
- Workflows decide “when” to start services and “what” to do next.
- Services decide “how” to watch, proxy, notify, and throttle.

## Errors & Logging
- All services surface clear, actionable logs.
- Failures bubble to the CLI with non‑zero exit codes when unrecoverable.

## Related Docs
- Solution overview — [solution](../solution.md)
- Engine — [engine](../engine/engine.md)
- Servers — [servers](../servers/servers.md)
- Pipelines — [pipelines](../../pipelines/pipelines.md)
- Workspace — [workspace](../workspace/workspace.md)
- CLI — [cli](../cli/cli.md)

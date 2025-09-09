# Watch

Run an initial build and tests, start dev servers, and react to changes with targeted rebuilds and live reload.

## Purpose
- Fast feedback loop during development.
- Keep dev server and API in sync with file changes.

## When To Use
- Default local workflow while editing.

## CLI
- `webstir watch` (also the default when no command is provided)

## Steps
1. Run `build`.
2. Run `test`.
3. Start the dev web server (serves `build/client/**`, SSE reload, clean URLs).
4. Start the Node API server from `build/server/index.js` and configure the proxy at `/api/*`.
5. Start watching `src/**`; ignore `build/**` and `dist/**`.
6. Route changes:
   - Client change → client pipelines → write to `build/client/**` → broadcast SSE reload.
   - Server change → server compile → restart Node process.
   - Shared change → rebuild affected client and server targets.

## Outputs
- Live dev server URL printed on startup.
- Incremental updates to `build/**` as files change.

## Stop & Recovery
- Stop with Ctrl+C; services shut down gracefully.
- Persistent server errors are throttled to avoid restart loops; errors are logged.

## Errors & Exit Codes
- Startup failures return non-zero (build/test/server bind errors). Runtime errors are logged.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Services — [services](../explanations/services.md)
- Servers — [servers](../explanations/servers.md)
- Engine — [engine](../explanations/engine.md)
- Workspace — [workspace](../explanations/workspace.md)

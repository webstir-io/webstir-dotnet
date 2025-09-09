# Dev Service

Hosts the development web server, live reload, and API proxy. Runs during the `watch` workflow.

## Responsibilities
- Serve `build/frontend/**` over HTTP with clean URLs.
- Expose an SSE endpoint to notify connected browsers to reload after frontend rebuilds.
- Proxy `/api/*` to the Node server that runs `build/backend/index.js`.
- Apply sensible cache headers in dev (HTML not cached; assets short-TTL).

## Lifecycle
1. `watch` runs `build` and `test`.
2. Start web server (serves frontend build) and start Node API server.
3. Begin watching `src/**`; on changes:
   - Frontend change → rebuild affected assets → broadcast SSE reload.
   - Backend change → compile backend → restart Node process.
   - Shared change → trigger both as needed.

## Ports & Env
- Web server prints the URL on startup; picks a free port or uses a configured one.
- Node server respects common env vars:
  - `PORT`
  - `WEB_SERVER_URL`
  - `API_SERVER_URL`

## Errors & Resilience
- Clear logs on failures; unrecoverable startup errors return non-zero exit codes via the CLI.
- Node server restarts are throttled to avoid loops on persistent errors.
- Proxy returns actionable errors if the API target is down.

## Related Docs
- Services overview — services.md
- Servers — servers.md
- Engine — engine.md
- Workflows — ../reference/workflows.md
- Workspace & paths — workspace.md

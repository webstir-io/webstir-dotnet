# Servers

Development and runtime servers used by Webstir.

## Overview
- Dev Web Server: ASP.NET Core app that serves built client assets, provides live reload via SSE, and applies clean URLs with dev caching.
- Node API Server: Runs the compiled server entry (`build/server/index.js`), restarted on changes.
- Proxy: In dev, the web server proxies `/api/*` to the Node process.

See also: [Engine](../engine/engine.md) and [Services](../services/services.md).

## Dev Web Server
- Serves `build/client/**` during `watch`.
- Clean URLs: `/about` serves `/pages/about/index.html`; `/` serves `/pages/home/index.html`.
- Live reload: SSE endpoint notifies connected browsers after client rebuilds.
- Caching: static assets cache with short TTL in dev; HTML not cached.
- Logs: prints server URL and proxy target on startup.

## Node API Server
- Entry: `build/server/index.js` produced by the server compile step.
- Lifecycle: spawned by the `watch` workflow; restarted on server file changes.
- Environment: respects `PORT` and base URL env variables used by the template.
- Health: template exposes `GET /api/health`.

## Proxy Rules
- Path: `/api/*`.
- Method/headers/body are forwarded as‑is to the Node server.
- Errors: if the Node server is down, proxy responses reflect connection failure clearly.

## Production (Sandbox)
- For production‑like testing, use the Docker sandbox: nginx serves `dist/client/**` and forwards to the Node API. See [Sandbox](../sandbox/sandbox.md).

## Errors & Resilience
- Dev server survives client rebuilds and continues serving.
- Proxy returns actionable messages if the API is unavailable.
- Node server restarts are throttled to avoid loops on persistent errors.

## Related Docs
- Solution overview — [solution](../solution.md)
- Engine — [engine](../engine/engine.md)
- Services — [services](../services/services.md)
- Sandbox — [sandbox](../sandbox/sandbox.md)
- Pipelines — [pipelines](../../pipelines/pipelines.md)
- CLI — [cli](../cli/cli.md)

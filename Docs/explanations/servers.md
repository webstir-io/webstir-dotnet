# Servers

Development and runtime servers used by Webstir.

## Overview
- Dev Web Server: ASP.NET Core app that serves built frontend assets, provides live reload via SSE, and applies clean URLs with dev caching.
- Node API Server: Runs the compiled backend entry (`build/backend/index.js`), restarted on changes.
- Proxy: In dev, the web server proxies `/api/*` to the Node process.

See also: [Engine](engine.md) and [Services](services.md).

## Dev Web Server
- Serves `build/frontend/**` during `watch`.
- Clean URLs: `/about` serves `/pages/about/index.html`; `/` serves `/pages/home/index.html`.
- Live reload: SSE endpoint notifies connected browsers after frontend rebuilds.
- Caching: static assets cache with short TTL in dev; HTML not cached.
- Logs: prints server URL and proxy target on startup.
- Client Errors: accepts `POST /client-errors` (JSON, <=32KB). Returns `204` on success, `415` for non-JSON, `413` if too large. Forwards to `ErrorTrackingService` and includes correlation id.

### Prod‑parity Toggles (Kestrel only)
The following flags affect only the ASP.NET Core dev server. They do nothing for nginx/S3/CloudFront.

- `Engine:AppSettings:EnableSecurityHeaders`: adds CSP and standard security headers.
- `Engine:AppSettings:EnablePrecompression`: serves precompressed `.br` files when available.
- `Engine:AppSettings:EnableEarlyHints`: attempts HTTP 103 Early Hints and also adds `Link` headers to final responses.

Defaults are `false` for fast, simple dev. Turn them on only when you want to simulate production locally.

## Node API Server
- Entry: `build/backend/index.js` produced by the backend compile step.
- Lifecycle: spawned by the `watch` workflow; restarted on backend file changes.
- Environment: respects `PORT` and base URL env variables used by the template.
- Health: template exposes `GET /api/health`.

## Proxy Rules
- Path: `/api/*`.
- Method/headers/body are forwarded as-is to the Node server.
- Errors: if the Node server is down, proxy responses reflect connection failure clearly.

## Production (Sandbox)
- For production-like testing, use the Docker sandbox: nginx serves `dist/frontend/**` and forwards to the Node API. See [Sandbox](../how-to/sandbox.md).

## Errors & Resilience
- Dev server survives frontend rebuilds and continues serving.
- Proxy returns actionable messages if the API is unavailable.
- Node server restarts are throttled to avoid loops on persistent errors.

## Related Docs
- Solution overview — [solution](solution.md)
- Engine — [engine](engine.md)
- Services — [services](services.md)
- Sandbox — [sandbox](../how-to/sandbox.md)
- Pipelines — [pipelines](pipelines.md)
- CLI — [cli](../reference/cli.md)

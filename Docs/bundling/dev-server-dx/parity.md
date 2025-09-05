# Dev Server & DX

## Current (Webstir)
- Static server with clean URL rewrites and API proxy (`Engine/Servers/WebServer.cs`, `Engine/Middleware/ApiProxyMiddleware.cs`).
- Live reload via SSE; no HMR; change watcher with debounce and server restarts (`Engine/Services/*`).
- Error logging to console; no in‑browser overlay.
- HTTPS: not configured.

## Baseline (Others)
- Vite/Webpack/Rspack: HMR/Fast Refresh, error overlay, proxy, HTTPS, history fallback.
- esbuild dev: fast rebuilds, HMR via plugins, error overlay via integrations.
- Turbopack: near‑instant HMR and on‑demand compilation.

## Gaps to Parity
- (P1) HMR for CSS and ESM (accept/decline boundaries, React Fast Refresh integration).
- (P2) Pretty diagnostics with overlay and clickable stack frames.
- (P5) HTTPS and flexible proxy rules; CORS helpers.
- File watching with smarter graph‑based invalidation.

## Parity Plan
- Implement ESM‑based HMR protocol and CSS hot swap; add React Fast Refresh bridge.
- Add error overlay script injected in dev HTML; wire to SSE/HMR channel.
- Add HTTPS option and richer proxy config (headers, path rewrites).
- Track per‑module dependencies to limit rebuild/invalidation scope.

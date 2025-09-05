# API Runtime

## Current (Webstir)
- Node server process runner with env (`PORT`, `WEB_SERVER_URL`, `API_SERVER_URL`).
- ASP.NET Core web server proxies `/api/*` to the Node server.
- No file‑based API routes, typed RPC, or client SDK generation.

## Baseline (Others)
- Next.js: Route handlers under `app/api/*` with Request/Response; edge/runtime options.
- Remix/SvelteKit: `+server.ts` per route; universal request handling; type‑safe fetchers via conventions.

## Gaps to Parity
- (P1) File‑based API routes under `src/server/api/**` with HTTP method exports.
- (P2) Typed client SDK generation from route/module signatures.
- (P3) Middleware hooks (auth, rate limit) and per‑route runtime hints (edge/node).

## Parity Plan
- Implement loader for `src/server/api/**/*.ts` exporting `GET/POST/...` handlers.
- Generate a typed client SDK into `build/client/_sdk.ts` with tree‑shakable functions.
- Add middleware pipeline: `before`, `after`, and error hooks; integrate with proxy.


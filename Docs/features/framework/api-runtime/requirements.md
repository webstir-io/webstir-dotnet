# Requirements: API Runtime

- Scope: file‑based API routes with typed request/response helpers and middleware.

## Customer Requirements (CR)
- Add API endpoints by creating files; no extra wiring.
- Typed helpers for requests/responses; clear error handling.
- Middleware for auth, rate limiting, and logging.

## Product Requirements (PR)
- Convention: `src/server/api/**` with modules exporting HTTP method handlers (`GET`, `POST`, ...).
- Middleware pipeline: `before`, `after`, and error handlers; per‑route configuration.
- Typed helpers for parsing JSON, form, and URL params; helpers for common responses.
- Optional runtime hint per route (node/edge) for future adapters.

## Software Requirements (SR)
- Discover and mount API modules; map file path to route path; dispatch by method.
- Provide `RequestContext` (url, params, query, cookies, headers, user/session).
- Uniform error formatting; return 405 for unsupported methods.
- Integrate with dev proxy; do not require extra processes to add routes.

## Acceptance Criteria
- A simple `GET` module returns JSON under its mapped route.
- Missing method returns 405; missing route returns 404.
- Middleware can block unauthorized requests and log requests.

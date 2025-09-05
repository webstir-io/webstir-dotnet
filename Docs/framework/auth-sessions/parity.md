# Auth & Sessions

## Current (Webstir)
- No built-in auth/session primitives; rely on API server code.
- Cookie passthrough via proxy; no helpers.

## Baseline (Others)
- Remix/Next/SvelteKit: cookie/session helpers, adapters for stores, CSRF patterns, route protection helpers.

## Gaps to Parity
- (P2) Cookie utilities (signing, parsing) and typed session interface.
- (P3) Auth guards and route protection helpers; login/logout flows.
- (P4) Adapters: memory/redis/session cookie configuration.

## Parity Plan
- Provide `cookies` and `session` helpers on server contexts; minimal, framework-agnostic types.
- Add guard helpers (`requireUser`, `redirectIfAuthed`) integrated with routing.
- Document integration with popular auth providers; keep core minimal.


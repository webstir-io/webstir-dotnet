# Forms & Actions

## Current (Webstir)
- No first‑class actions; form handling left to custom JS and API endpoints.

## Baseline (Others)
- Remix: progressive enhancement for forms with `action` functions and data mutations.
- Next.js: Server Actions (form submissions/invocations), CSRF patterns, validation helpers.

## Gaps to Parity
- (P1) Route‑scoped action handlers with typed inputs/outputs.
- (P2) Progressive enhancement: submit without JS; enhance with fetch when available.
- (P3) CSRF, validation, and error propagation patterns.

## Parity Plan
- Convention: `pages/<route>/action.server.ts` exports action handlers keyed by method.
- Emit an endpoint under `/__actions/<route>` in dev/prod; auto‑wire via proxy.
- Provide helpers: `parseForm()`, `validate(schema)`, `csrf()`; surface errors to page state.


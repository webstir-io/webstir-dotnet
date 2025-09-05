# Framework

Hub for routing, layouts, data, actions, SSR/SSG/streaming, API runtime, auth, client navigation, config/env, and deployment. This doc describes the feature set, indexes requirements by sub‑area, and tracks a prioritized backlog.

## Guardrails
- Platform‑first: MPA‑first with optional SPA navigation; progressive enhancement preferred.
- Minimal deps: avoid heavy client runtimes; keep libraries optional and replaceable.
- Simplicity: file‑based routing and small, explicit APIs; predictable conventions.
- Zero‑config: sensible defaults; config is opt‑in and not required for correctness.
- Optional SSR/SSG: easy to adopt; never required to render a first page.
- Data & actions: typed contracts; co‑located; progressive enhancement without mandatory JS.
- Auth & sessions: cookie/session primitives; avoid tight coupling to third‑party auth providers.
- Config & env: safe exposure rules; explicit modes (dev/prod/test).
- Security: validate inputs; secure cookies; document CSRF patterns for forms/actions.
- DX: one‑command dev; clear errors; no framework‑coupled Fast Refresh assumptions.
- Extensibility: optional hooks; avoid a plugin ecosystem until core is solid.

## Requirements Index
- Routing & Pages — [./routing-pages/requirements.md](./routing-pages/requirements.md)
- Layouts & Metadata — [./layouts-metadata/requirements.md](./layouts-metadata/requirements.md)
- Data Fetching — [./data-fetching/requirements.md](./data-fetching/requirements.md)
- Forms & Actions — [./forms-actions/requirements.md](./forms-actions/requirements.md)
- API Runtime — [./api-runtime/requirements.md](./api-runtime/requirements.md)
- Client Navigation — [./client-navigation/requirements.md](./client-navigation/requirements.md)
- SSR/SSG/Streaming — [./ssr-ssg-streaming/requirements.md](./ssr-ssg-streaming/requirements.md)
- Auth & Sessions — [./auth-sessions/requirements.md](./auth-sessions/requirements.md)
- Config & Env — [./config-env/requirements.md](./config-env/requirements.md)
- Deployment — [./deployment/requirements.md](./deployment/requirements.md)

## Prioritized Backlog
1. File‑based routing (static/dynamic/nested) — [Routing & Pages](./routing-pages/requirements.md)
2. Layouts, partials, and metadata API — [Layouts & Metadata](./layouts-metadata/requirements.md)
3. Data loading contracts (server/client) with caching — [Data Fetching](./data-fetching/requirements.md)
4. Form actions with progressive enhancement + validation — [Forms & Actions](./forms-actions/requirements.md)
5. API routes and server runtime conventions (typed SDK) — [API Runtime](./api-runtime/requirements.md)
6. Optional client navigation (SPA‑style links, prefetch) — [Client Navigation](./client-navigation/requirements.md)
7. SSR/SSG and streaming primitives (hydration maps) — [SSR/SSG/Streaming](./ssr-ssg-streaming/requirements.md)
8. Auth, cookies, and session primitives — [Auth & Sessions](./auth-sessions/requirements.md)
9. Config, env, and mode handling (dev/prod/test) — [Config & Env](./config-env/requirements.md)
10. Deployment adapters and output targets — [Deployment](./deployment/requirements.md)

Notes
- SSR/SSG relies on bundling work in features/bundling/platform-advanced.
- Keep MPA as a first‑class mode; SPA navigation is opt‑in.

# Framework

This folder tracks framework feature parity: routing, layouts, data, actions, SSR, and runtime. Each category documents current behavior, baseline in popular frameworks, gaps, and a concrete plan.

## Prioritized Backlog
1. File‑based routing (static/dynamic/nested) — [Routing & Pages](./routing-pages/parity.md)
2. Layouts, partials, and metadata API — [Layouts & Metadata](./layouts-metadata/parity.md)
3. Data loading contracts (server/client) with caching — [Data Fetching](./data-fetching/parity.md)
4. Form actions with progressive enhancement + validation — [Forms & Actions](./forms-actions/parity.md)
5. API routes and server runtime conventions (typed SDK) — [API Runtime](./api-runtime/parity.md)
6. Optional client navigation (SPA‑style links, prefetch) — [Client Navigation](./client-navigation/parity.md)
7. SSR/SSG and streaming primitives (hydration maps) — [SSR/SSG/Streaming](./ssr-ssg-streaming/parity.md)
8. Auth, cookies, and session primitives — [Auth & Sessions](./auth-sessions/parity.md)
9. Config, env, and mode handling (dev/prod/test) — [Config & Env](./config-env/parity.md)
10. Deployment adapters and output targets — [Deployment](./deployment/parity.md)

Notes
- SSR/SSG relies on bundling work in Docs/bundling/platform-advanced.
- Keep MPA as a first‑class mode; SPA navigation is opt‑in.

# Routing & Pages

## Current (Webstir)
- Clean URLs rewrite to `pages/<name>/index.html` (MPA) via web server middleware.
- No nested routes, dynamic params, or catch‑all routes.
- No 404/500 route conventions; simple fallback behavior.

## Baseline (Others)
- Next.js/Remix/SvelteKit/Nuxt: file‑based routing, nested layouts, dynamic `[id]`, catch‑all `[...all]`, route groups, optional segments, and 404/500 conventions.
- Remix/SvelteKit: co‑located loaders/actions per route; route modules define metadata and error boundaries.

## Gaps to Parity
- (P1) File‑based router spec: static, dynamic, catch‑all, nested, and index routes.
- (P2) 404/500 conventions and per‑route error/loader boundaries.
- (P3) Route groups and optional segments.

## Parity Plan
- Define route manifest generated from `src/client/pages/**` and `src/server/routes/**`.
- Support `pages/<route>/index.html` and dynamic folders: `pages/users/[id]/index.html`, catch‑all `pages/docs/[...slug]/index.html`.
- Add 404/500 page conventions: `pages/_404/index.html`, `pages/_500/index.html`.
- Generate a JSON manifest for dev/publish; reuse for client navigation (opt‑in).


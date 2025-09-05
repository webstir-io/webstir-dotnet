# Client Navigation

## Current (Webstir)
- Multi‑page app: full navigations; no client router or prefetch.
- Live reload via SSE (dev); no HMR for route state.

## Baseline (Others)
- Next.js/Remix/SvelteKit: SPA‑style client navigation with prefetch, scroll restoration, and transition states.

## Gaps to Parity
- (P2) Optional client router with link component and history/scroll handling.
- (P3) Prefetch heuristics and partial hydration of route data.

## Parity Plan
- Provide a tiny router runtime: `Link`, `navigate`, `useRoute`, `useParams` that works on top of the file‑based routing manifest.
- Enable opt‑in prefetch on `Link` and intersection‑based heuristics.
- Keep MPA default; client router is additive and removable.


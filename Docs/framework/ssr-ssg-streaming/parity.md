# SSR/SSG/Streaming

## Current (Webstir)
- No SSR/SSG pipeline; client pages rendered statically from HTML fragments.
- Node server exists for APIs only.

## Baseline (Others)
- Next.js/Remix/SvelteKit/Astro: SSR, SSG, ISR/revalidate; streaming (React/Suspense), per‑route rendering modes.

## Gaps to Parity
- (P1) Basic SSR for HTML assembly with loader props and hydration map.
- (P2) SSG for static routes and data; incremental revalidation.
- (P3) Streaming support (React/Suspense) and progressive HTML head updates.

## Parity Plan
- Add SSR entry that renders pages on the Node server using the route manifest and `load.server.ts` props.
- Emit hydration manifest listing islands/assets; coordinate with bundling runtime.
- Provide SSG mode during publish to pre‑render routes; record revalidate hints.


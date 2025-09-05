# Data Fetching

## Current (Webstir)
- No framework data loaders; pages are static HTML + per‑page JS.
- Node server runs separately for APIs; proxy at `/api/*` in dev.

## Baseline (Others)
- Remix/SvelteKit: route `loader` functions (server) returning props; invalidation and caching hints.
- Next.js: server components, `fetch` caching/revalidation, route handlers; `getServerSideProps`/`generateStaticParams` (legacy).

## Gaps to Parity
- (P1) Per‑route server loader contract with typed props and serialization.
- (P2) Cache and revalidation directives; opt‑in client prefetch.
- (P3) Shared utilities for fetching with cookies/auth context.

## Parity Plan
- Add `pages/<route>/load.server.ts` returning serializable props for hydration; generate `props.json` in SSG and embed in SSR.
- Provide `cache(seconds)`, `revalidate(tag|seconds)`, and `noStore()` helpers.
- Pass request context (cookies, headers, params) into loaders.


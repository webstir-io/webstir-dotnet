# Requirements: Data Fetching

- Scope: co‑located server loaders with caching and revalidation semantics.

## Customer Requirements (CR)
- Fetch data on the server per route and pass typed props to pages.
- Control caching and revalidation with simple helpers.
- Use request context (params, cookies, headers) inside loaders.

## Product Requirements (PR)
- Convention: `pages/<route>/load.server.ts` exports a default async function returning serializable props.
- Helpers: `cache(seconds)`, `revalidate(tag|seconds)`, `noStore()`.
- Pass `{ params, cookies, headers, url }` into loaders.
- SSG writes props JSON; SSR embeds props in HTML.

## Software Requirements (SR)
- Execute loaders on the server; validate that returned props are serializable.
- Record caching directives and expose to server/SSG runtime.
- Provide a way to tag and invalidate data for revalidation.

## Acceptance Criteria
- A page with `load.server.ts` receives props in SSR and SSG builds.
- `cache()` and `revalidate()` affect responses and rebuilds as documented.
- Loader errors appear in dev overlay and fail builds when unhandled.

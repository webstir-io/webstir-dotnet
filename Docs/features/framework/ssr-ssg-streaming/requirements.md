# Requirements: SSR/SSG/Streaming

- Scope: server rendering, static generation, optional streaming, hydration maps.

## Customer Requirements (CR)
- Render pages on the server for better TTFB when needed.
- Pre‑render static routes at build time.
- Hydrate client code selectively; keep it optional.

## Product Requirements (PR)
- SSR entry that renders routes using the manifest and loader props.
- SSG mode that writes HTML + props for static routes; supports revalidation.
- Hydration manifest that maps islands/chunks to routes.
- Optional streaming support where the view layer permits.

## Software Requirements (SR)
- Build a server bundle for SSR; provide a `render(request)` API.
- During publish, run SSG for configured routes and embed props.
- Generate a hydration manifest consumed by the client runtime.
- Respect revalidation hints from data loaders.

## Acceptance Criteria
- A sample route renders via SSR with loader props and returns full HTML.
- SSG produces HTML files for static routes; props load on navigation.
- Hydration loads only required chunks per route.

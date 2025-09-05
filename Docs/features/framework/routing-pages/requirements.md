# Requirements: Routing & Pages

- Scope: file‑based routing: static, dynamic, nested, catch‑all; 404/500.

## Customer Requirements (CR)
- Predictable URLs from files with zero config.
- Dynamic params and catch‑all routes that are easy to use.
- Nested routes with index pages and clear 404/500 conventions.
- A manifest that other features (navigation/SSR) can consume.

## Product Requirements (PR)
- Map `src/client/pages/**` to routes:
  - Static: `pages/about/index.html` → `/about`.
  - Dynamic: `pages/users/[id]/index.html` → `/users/:id`.
  - Catch‑all: `pages/docs/[...slug]/index.html` → `/docs/*`.
  - Index: `pages/index.html` → `/`.
- Conventions: 404 as `pages/_404/index.html`, 500 as `pages/_500/index.html`.
- Generate a route manifest JSON for dev/build.
- Extract params from URLs and expose them to loaders/actions/SSR.

## Software Requirements (SR)
- Scan the pages folder and build a deterministic route tree.
- Define param parsing rules and ordering (static > dynamic > catch‑all).
- Watch for file changes in dev and update the manifest.
- Provide helpers to resolve route → file and file → route.

## Acceptance Criteria
- Static, dynamic, and catch‑all routes resolve and render the correct files.
- Visiting a missing route renders the 404 page; server errors use 500.
- The manifest lists all routes with their param patterns and file paths.

# Requirements: Deployment

- Scope: adapters for Node and static hosts; output targets and assets mapping.

## Customer Requirements (CR)
- Deploy SSR/Actions to a Node server with minimal wiring.
- Deploy SSG/MPA sites to static hosts easily.
- Clear docs for environment and asset serving.

## Product Requirements (PR)
- Node adapter that mounts SSR renderer and actions under a single entry.
- Static adapter that emits pre‑rendered HTML, props, and redirects for dynamic routes.
- Asset mapping and environment variable wiring guidance.

## Software Requirements (SR)
- Produce a server entry compatible with popular Node hosts.
- Emit static outputs with a manifest for assets and redirects.
- Provide deployment docs/templates for common platforms.

## Acceptance Criteria
- A sample SSR app runs on a Node host using the adapter.
- A sample SSG/MPA site deploys to a static host serving correct assets.

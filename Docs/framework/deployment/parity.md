# Deployment

## Current (Webstir)
- Dev: ASP.NET Core web server + Node API server with proxy.
- Prod: Static client output; no SSR/SSG deployment adapters.

## Baseline (Others)
- Next.js/Remix/SvelteKit: adapters for Node, edge, static, serverless; environment wiring and asset serving.

## Gaps to Parity
- (P3) Node adapter for SSR/Actions runtime; static adapter for SSG/MPA.
- (P4) Edge/serverless hints (where applicable) and middleware hooks.

## Parity Plan
- Provide a Node adapter that mounts SSR renderer and actions under a single server entry.
- Provide a static adapter that emits pre‑rendered HTML + JSON props and redirects for dynamic routes.
- Document deployment targets and environment contracts.


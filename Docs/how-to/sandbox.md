# Sandbox

Run a published Webstir app behind nginx with the template API server via Docker Compose. Use this to validate `dist/` outputs and production routing without the dev server.

## Overview
- Two containers: `web` (nginx) and `api` (Node server).
- Clean URLs: nginx maps `/about` → `/pages/about/index.html` and page assets.
- API proxy: browser calls `http://localhost:8000` directly to the API container.
- Volumes mount published assets from your local build.

Files:
- Compose — `Sandbox/docker-compose.yml`
- nginx config — `Sandbox/web/nginx.conf`

## Prerequisites
- Docker and Docker Compose installed.
- Published client and compiled server available locally:
  - Client: `dist/client/**` (from `webstir publish`)
  - Server: `build/server/index.js` (from `webstir build` or `watch`)

The provided compose mounts a seed output under `../CLI/out/seed/...`. Swap to your project paths as needed.

## Usage
- From `Sandbox/` run: `docker compose up -d`
- Open: `http://localhost:8080` (web) and `http://localhost:8000` (api)
- Logs: `docker compose logs -f web` or `docker compose logs -f api`
- Stop: `docker compose down`

## Volumes & Paths
Default mounts (see compose):
- Web root: `../CLI/out/seed/dist/client` → `/usr/share/nginx/html`
- App assets: `../CLI/out/seed/dist/client/app` → `/usr/share/nginx/html/app`
- API app root: `../CLI/out/seed` → `/app` (runs `build/server/index.js`)

To use your project, point volumes at your project’s `dist/client` and built server directory.

## Nginx Behavior
- Clean URLs: `/` serves `pages/home/index.html`; `/about` serves `pages/about/index.html`.
- Page assets: `/index.*.(css|js)` map to `pages/home/*`; `/about/*` map to `pages/about/*`.
- Static assets: cached for 7 days; HTML is not cached.
- Source maps: not expected in publish; config serves only published assets.

## Environment
API container env (edit in compose if needed):
- `PORT=8000`
- `WEB_SERVER_URL=http://web`
- `API_SERVER_URL=http://api:8000`

## Expected Dist Layout
Produced by `webstir publish`:
- `dist/client/pages/<page>/index.html`
- `dist/client/pages/<page>/index.<timestamp>.{css|js}`
- `dist/client/pages/<page>/manifest.json`
- Shared app assets under `dist/client/app/*`

## Troubleshooting
- 404s for pages: ensure `dist/client/pages/<page>/index.html` exists and volumes point to the correct `dist` path.
- 404s for assets: verify fingerprinted files exist and nginx mapping matches page name.
- API not reachable: check `api` logs and that `build/server/index.js` exists in the mounted directory.
- Caching issues: hard refresh; nginx caches static assets for 7 days by default.

## Related Docs
- Solution overview — [solution](../explanations/solution.md)
- CLI reference — [cli](../reference/cli.md)
- Engine internals — [engine](../explanations/engine.md)
- Pipelines (publish details) — [pipelines](../explanations/pipelines.md)
- Tests (publish E2E) — [tests](../explanations/testing.md)

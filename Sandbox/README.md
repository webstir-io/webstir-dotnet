Webstir Sandbox

Run a published Webstir project via Docker Compose. The web container serves the production client from `CLI/out/seed/dist/client`, and the API container runs the template Node server compiled at `CLI/out/seed/build/server/index.js`.

Requirements
- Docker and Docker Compose
- This repo built/published client into `CLI/out/seed/dist/client`
- Seed server compiled at `CLI/out/seed/build/server/index.js` (present by default)

Quick Start
1) Produce a published client from the seed:
   - `./scripts/deploy-seed.sh` (init → build → publish)

2) Up the stack:
   - From repo root: `docker compose -f Sandbox/docker-compose.yml up --build`
   - Web: http://localhost:8080
   - API: http://localhost:8000 (e.g., GET /api/health)

3) (Optional) Start the local npm registry:
   - `docker compose -f Sandbox/docker-compose.yml up registry`
   - The compose stack also runs an `npmrc` helper (writes `Sandbox/npmrc/.npmrc`) and a `publisher` helper that publishes `@electric-coding-llc/webstir-frontend` and `@electric-coding-llc/webstir-test` if they are missing.
   - The Verdaccio container uses `bcrypt` for htpasswd entries via `Sandbox/verdaccio/config.yaml`; tweak the config if you need different access rules.
   - Manual regeneration: `docker compose -f Sandbox/docker-compose.yml run --rm npmrc`
   - Credentials default to `webstir` / `webstir` / `dev@local.test`; override via env vars (`NPM_USERNAME`, `NPM_PASSWORD`, `NPM_EMAIL`, `NPM_SCOPE`, `NPM_REGISTRY`).
   - The helper also copies the file into `Sandbox/npmrc/host/.npmrc`; change the destination by exporting `NPMRC_HOST_DIR=/path/to/dir` (e.g., `export NPMRC_HOST_DIR=$HOME`).
   - After the helpers run, `WEBSTIR_PACKAGE_SOURCE=registry webstir install` will pull from the local registry.

Layout Expectations
- `CLI/out/seed/dist/client`: Published client files (index.html, timestamped js/css) under page folders.
- `CLI/out/seed/build/server`: Compiled Node server (index.js) that responds to `/api/health`.

Notes
- The API is CORS-permissive toward the web container host per the template.
- Adjust ports or hostnames by editing `Sandbox/docker-compose.yml`.
- Verdaccio stores packages in the `registry-storage` Docker volume; remove the volume to reset the registry.
- For real deployments, you may collapse to a single reverse-proxied entrypoint or serve static content via a CDN and keep API separate.

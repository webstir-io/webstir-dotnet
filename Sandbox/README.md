Webstir Sandbox

Run a published Webstir project via Docker Compose. The web container serves the production client from `CLI/out/seed/dist/client`, and the API container runs the template Node server compiled at `CLI/out/seed/build/server/index.js`.

Requirements
- Docker and Docker Compose
- This repo built/published client into `CLI/out/seed/dist/client`
- Seed server compiled at `CLI/out/seed/build/server/index.js` (present by default)

Quick Start
1) Produce a published client from the seed:
   - `./utilities/scripts/deploy-seed.sh` (init → build → publish)

2) Up the stack:
   - From repo root: `docker compose -f Sandbox/docker-compose.yml up --build`
   - Web: http://localhost:8080
   - API: http://localhost:8000 (e.g., GET /api/health)

Layout Expectations
- `CLI/out/seed/dist/client`: Published client files (index.html, timestamped js/css) under page folders.
- `CLI/out/seed/build/server`: Compiled Node server (index.js) that responds to `/api/health`.

Notes
- The API is CORS-permissive toward the web container host per the template.
- Adjust ports or hostnames by editing `Sandbox/docker-compose.yml`.
- For real deployments, you may collapse to a single reverse-proxied entrypoint or serve static content via a CDN and keep API separate.

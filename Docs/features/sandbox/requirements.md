# Requirements: Sandbox & Docker

- Scope: Docker Compose sandbox to serve the published client and run the API server.

## Customer Requirements (CR)
- Run the seed app locally via Docker with minimal steps.
- Access the web UI and a simple API endpoint.

## Product Requirements (PR)
- Docker Compose file with two services: web (static) and api (node).
- Map published client files into the web container and compiled server into api.
- Expose default ports (web: 8080, api: 8000) with ability to change.

## Software Requirements (SR)
- `Sandbox/docker-compose.yml` defines services, volumes, and ports.
- `Sandbox/README.md` documents prerequisites and quick start.
- Compose up works after running `scripts/deploy-seed.sh`.

## Acceptance Criteria
- After seed publish, `docker compose up` serves the site at http://localhost:8080.
- API responds to `/api/health` at http://localhost:8000.
- Changing ports in compose reflects in service endpoints.


# Webstir

Modern, minimal full-stack TypeScript web framework and build tool. Webstir scaffolds projects, builds client and server code, runs a fast dev server with live reload and API proxying, and produces optimized production bundles.

## Status
- Experimental and evolving — APIs, defaults, and workflows may change between releases while the ecosystem settles.
- Not yet recommended for production workloads; see the docs mission and vision for more context.

## Highlights
- Simple CLI: `init`, `watch` (default), `build`, `publish`, `add-page`, `add-route`, `add-job`, `help`
- Full-stack by default: client + server + shared types
- Dev server with live reload (SSE) and `/api` proxy to Node server
- TypeScript-first: project references; single `tsc --build` for client/server/shared
- HTML assembly and minification: template merge + safe, always-on HTML minifier
- JS bundling: ESM only, concatenation + tree-shaking + minification
- CSS pipeline: plain CSS and CSS Modules, import graph, autoprefix + minify
- Asset manifest per page with timestamped filenames for cache busting
  and precompressed `.html.br`, `.css.br`, `.js.br` artifacts

## Prerequisites
- .NET SDK 10.0+
- Node.js 20.18+ and npm
- TypeScript compiler available on PATH (`tsc`), e.g. `npm i -g typescript`

## Quick Start
```bash
# From repo root (local run)
# 1) Create a new project (fullstack by default)
dotnet run --project CLI -- init my-app

# 2) Start dev mode (same as running with no command)
dotnet run --project CLI -- watch --project my-app
# or
dotnet run --project CLI -- --help
```

To build a single self-contained binary:
```bash
./publish.sh
# Produces a single-file executable named `webstir`
```

Getting started with usage and concepts:
- Tutorials: [Getting Started](docs/tutorials/getting-started.md), [Your First App](docs/tutorials/first-app.md)
- CLI reference: [docs/reference/cli.md](docs/reference/cli.md)

## Docs
- Overview and index: [docs/README.md](docs/README.md)
- Tutorials: [docs/tutorials/README.md](docs/tutorials/README.md)
- How-to guides: [docs/how-to/README.md](docs/how-to/README.md)
- Reference (CLI, workflows, templates, contracts): [docs/reference/README.md](docs/reference/README.md)
- Explanations (engine, pipelines, services, servers, workspace, testing): [docs/explanations/README.md](docs/explanations/README.md)

## Project Structure
```
src/
├─ frontend/          # Frontend app (HTML/CSS/TS)
│  ├─ app/            # Base template (app.html, app.css, app.ts, refresh.js)
│  ├─ pages/<name>/   # Per-page index.html/css/ts
│  ├─ images/         # Static images (png, jpg, jpeg, gif, svg, webp, ico)
│  ├─ fonts/          # Web fonts (woff2, woff, ttf, otf, eot, svg)
│  └─ media/          # Media (mp3, m4a, wav, ogg, mp4, webm, mov)
├─ backend/           # Backend TypeScript (compiled to build/backend, run by Node)
└─ shared/            # Shared types and utilities

build/                # Dev build output
└─ frontend/          # Served by the dev server
   ├─ pages/**
   ├─ images/**
   ├─ fonts/**
   └─ media/**

dist/                 # Production output
└─ frontend/
   ├─ pages/<name>/
   │  ├─ index.html
   │  ├─ index.<timestamp>.js
   │  ├─ index.<timestamp>.css
   │  └─ manifest.json   # { js, css }
   ├─ images/**
   ├─ fonts/**
   └─ media/**
```

## Development Server
- Web server (ASP.NET Core) serves `build/frontend` at `http://localhost:8088`
  - Injects SSE endpoint for reload notifications
  - Proxies `/api/*` to the Node server
  - To listen on your LAN, set `AppSettings__WebServerHost=0.0.0.0` (defaults to `localhost`)
- Node server runs compiled `build/backend/index.js` on `http://localhost:8008`
  - Waits for the `API server running` readiness line and hits `/api/health` before reporting success
  - Tuning flags:
    - `WEBSTIR_BACKEND_WAIT_FOR_READY=skip` — skip waiting for the readiness log line
    - `WEBSTIR_BACKEND_READY_TIMEOUT_SECONDS` — override readiness wait timeout (default 30)
    - `WEBSTIR_BACKEND_HEALTHCHECK=skip` — skip the health probe entirely
    - `WEBSTIR_BACKEND_HEALTH_TIMEOUT_SECONDS` — per-attempt probe timeout (default 5)
    - `WEBSTIR_BACKEND_HEALTH_ATTEMPTS` — retries before failing (default 5)
    - `WEBSTIR_BACKEND_HEALTH_DELAY_MILLISECONDS` — delay between retries (default 250)
    - `WEBSTIR_BACKEND_HEALTH_PATH` — override the probe path (default `/api/health`)
    - `WEBSTIR_BACKEND_TERMINATION` — shutdown method for Node during watch (`ctrlc` or `kill`; default `kill`)
- API proxy default target updated accordingly
- Ports can be customized in `AppSettings` (when running the published binary) or via environment variables used by the Node server (`PORT`, `WEB_SERVER_URL`, `API_SERVER_URL`).

## Build & Publish Pipelines
- See [docs/explanations/pipelines.md](docs/explanations/pipelines.md) for HTML, CSS, JS/TS, and static asset (Images, Fonts, Media) stages and publish details.
  - Backend sourcemaps (publish): set `WEBSTIR_BACKEND_SOURCEMAPS=on` to emit `.map` files under `dist/backend/**` and retain a `//# sourceMappingURL` in `index.js`.

## Testing
- Philosophy and scope: [docs/explanations/testing.md](docs/explanations/testing.md)
- Repo harness: `dotnet test Tester/Tester.csproj` (set `WEBSTIR_TEST_MODE=full` for the complete suite)

## Sandbox
- Purpose: Run a published Webstir client alongside the seed API via Docker Compose.
- Docs: [docs/how-to/sandbox.md](docs/how-to/sandbox.md)
- Start: `docker compose -f Sandbox/docker-compose.yml up --build`
- Mounts: `CLI/out/seed/dist/frontend` (web), `CLI/out/seed` (api)

## CLI Usage Examples
In multi-project workspaces, append `--project <project>` (or `-p <project>`) to target a specific app.

- Routes
  - `webstir add-route users` — adds `GET /api/users` to `webstir.moduleManifest.routes` in `package.json`.
  - `webstir add-route users --method POST --path /api/users` — adds `POST /api/users`.
  - `webstir add-route accounts --fastify` — also scaffolds `src/backend/server/routes/accounts.ts` and registers it in `server/fastify.ts` when present.
  - `webstir add-route reports --summary "List reports" --tags analytics,reports` — seeds route metadata for downstream consumers.
  - `webstir add-route invoices --body-schema json-schema:Invoice@./schemas/invoice.json --response-status 201` — wires schema references into the manifest.

- Jobs
  - `webstir add-job cleanup` — creates `src/backend/jobs/cleanup/index.ts` and adds a jobs manifest entry.
  - `webstir add-job nightly --schedule "0 0 * * *"` — sets a schedule in the manifest.
  - `webstir add-job archive --description "Archive stale data" --priority 10` — adds metadata to the job manifest entry.

## Community & Support
- Code of Conduct: https://github.com/webstir-io/.github/blob/main/CODE_OF_CONDUCT.md
- Contributing guide for this repo: [CONTRIBUTING.md](./CONTRIBUTING.md)
- Security disclosures: https://github.com/webstir-io/.github/blob/main/SECURITY.md
- Support expectations and contact channels: https://github.com/webstir-io/.github/blob/main/SUPPORT.md

---

© 2025 Electric Coding LLC and contributors  

Licensed under the [MIT License](./LICENSE).  

Webstir™ is a trademark of Electric Coding LLC.  

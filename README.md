# Webstir

Modern, minimal full-stack TypeScript web framework and build tool. Webstir scaffolds projects, builds client and server code, runs a fast dev server with live reload and API proxying, and produces optimized production bundles.

## Highlights
- Simple CLI: `init`, `watch` (default), `build`, `publish`, `add-page`, `help`
- Full-stack by default: client + server + shared types
- Dev server with live reload (SSE) and `/api` proxy to Node server
- TypeScript-first: project references; single `tsc --build` for client/server/shared
- HTML assembly and minification: template merge + safe, always-on HTML minifier
- JS bundling: ESM only, concatenation + tree-shaking + minification
- CSS pipeline: plain CSS and CSS Modules, import graph, autoprefix + minify
- Asset manifest per page with timestamped filenames for cache busting
  and precompressed `.html.br`, `.css.br`, `.js.br` artifacts

## Prerequisites
- .NET SDK 9.0+
- Node.js 18+ (20+ recommended) and npm
- TypeScript compiler available on PATH (`tsc`), e.g. `npm i -g typescript`

## Quick Start
```bash
# From repo root (local run)
# 1) Create a new project (fullstack by default)
dotnet run --project CLI -- init my-app

# 2) Start dev mode (same as running with no command)
dotnet run --project CLI -- watch --project-name my-app
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
- Node server runs compiled `build/backend/index.js` on `http://localhost:8008`
- API proxy default target updated accordingly
- Ports can be customized in `AppSettings` (when running the published binary) or via environment variables used by the Node server (`PORT`, `WEB_SERVER_URL`, `API_SERVER_URL`).

## Build & Publish Pipelines
- See [docs/explanations/pipelines.md](docs/explanations/pipelines.md) for HTML, CSS, JS/TS, and static asset (Images, Fonts, Media) stages and publish details.

## Testing
- Philosophy and scope: [docs/explanations/testing.md](docs/explanations/testing.md)
- Repo harness: `dotnet run --project Tests` (see `Tests/Program.cs`)

## Sandbox
- Purpose: Run a published Webstir client alongside the seed API via Docker Compose.
- Docs: [docs/how-to/sandbox.md](docs/how-to/sandbox.md)
- Start: `docker compose -f Sandbox/docker-compose.yml up --build`
- Mounts: `CLI/out/seed/dist/frontend` (web), `CLI/out/seed` (api)

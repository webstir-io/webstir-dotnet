# Webstir

Modern, minimal fullstack TypeScript web framework and build tool. Webstir scaffolds projects, builds client and server code, runs a fast dev server with live reload and API proxying, and produces optimized production bundles.

## Highlights
- Simple CLI: `init`, `watch` (default), `build`, `publish`, `add-page`, `help`
- Fullstack by default: client + server + shared types
- Dev server with live reload (SSE) and `/api` proxy to Node server
- TypeScript-first: project references; single `tsc --build` for client/server/shared
- HTML assembly: template + per-page HTML
- JS bundling: ESM only, concatenation + tree‑shaking + minification
- CSS pipeline: plain CSS and CSS Modules, import graph, autoprefix + minify
- Asset manifest per page with timestamped filenames for cache busting

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

## Commands
- `watch`: Build and start dev services (default if no command provided)
  - Serves `build/client` on `http://localhost:8088` with live reload
  - Proxies `/api/*` to the Node server (default `http://localhost:8008`)
- `init [options] [directory]`: Initialize a new project
  - Options: `--client-only`, `--server-only`, `--project-name <name>`
- `build [options]`: Build once (client/server/shared as needed)
  - Options: `--clean`
- `publish`: Production build (minified assets + per-page manifest + HTML tidy)
- `add-page <page-name>`: Scaffolds a new page (HTML, CSS, TS)
- `help [command]`: Show CLI help

Examples:
```bash
# Initialize in a folder via positional argument
dotnet run --project CLI -- init my-app

# Build a specific project in the current directory
dotnet run --project CLI -- build --project-name my-app

# Add a page to an existing project
dotnet run --project CLI -- add-page about --project-name my-app
```

## Project Structure
```
src/
├─ client/            # Client app (HTML/CSS/TS)
│  ├─ app/            # Base template (app.html, app.css, app.ts, refresh.js)
│  └─ pages/<name>/   # Per-page index.html/css/ts
├─ server/            # Server TypeScript (compiled to build/server, run by Node)
└─ shared/            # Shared types and utilities

build/                # Dev build output
└─ client/            # Served by the dev server

dist/                 # Production output
└─ client/pages/<name>/
   ├─ index.html
   ├─ index.<timestamp>.js
   ├─ index.<timestamp>.css
   └─ manifest.json   # { js, css, map }
```

## Development Server
- Web server (ASP.NET Core) serves `build/client` at `http://localhost:8088`
  - Injects SSE endpoint for reload notifications
  - Proxies `/api/*` to the Node server
- Node server runs compiled `build/server/index.js` on `http://localhost:8008`
- API proxy default target updated accordingly
- Ports can be customized in `AppSettings` (when running the published binary) or via environment variables used by the Node server (`PORT`, `WEB_SERVER_URL`, `API_SERVER_URL`).

## Build & Publish Pipelines
- HTML: Validate base template, merge per-page content, strip dev-only scripts, collapse inter-tag whitespace (publish)
- JavaScript: ESM graph build, concatenation, tree‑shaking, minification; timestamped filename; manifest update
- CSS: Imports resolution, CSS Modules, URL rewriting, autoprefix, minification; timestamped filename; manifest update

## Testing
Webstir emphasizes workflow tests over unit tests.

Run tests:
```bash
# Quick (default)
dotnet run --project Tests

# Full suite (includes watch, deeper publish checks)
dotnet run --project Tests -- --full

# Run a specific workflow
dotnet run --project Tests -- test publish
```
See `docs/features/testing/testing.md` for the philosophy and index. For C# runner usage, see `docs/features/testing/cs-runner/running.md`.

## Sandbox
- Purpose: Run a published Webstir client alongside the seed API via Docker Compose.
- Docs: See `Sandbox/README.md`.
- Start: `docker compose -f Sandbox/docker-compose.yml up --build`
- Mounts: `CLI/out/seed/dist/client` (web), `CLI/out/seed` (api)

## Roadmap & Docs
- Backlog (aggregated): `docs/backlog.md`
- Feature hubs:
  - Bundling: `docs/features/bundling/bundling.md`
  - Framework: `docs/features/framework/framework.md`

## Notes & Limitations
- TypeScript is invoked via `tsc`; ensure it’s on your PATH.
- When multiple projects exist in the working directory, use `--project-name <name>` with commands.

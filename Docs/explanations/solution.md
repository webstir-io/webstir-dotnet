# Solution

Hub for the CLI and host tooling. This doc explains what the solution is, how it’s organized, the technology choices, and how the app works at a high level.

## What It Is
- Opinionated, zero-config build tool and project scaffolder.
- Full-stack by default: client (HTML/CSS/TS) + Node API server.
- Single CLI drives workflows: init, build, watch, test, publish, generators.
- Minimal dependencies; pipelines are orchestrated via C#.

## Organization
- [CLI](../reference/cli.md): Command entrypoint and help text. Wires DI and invokes workflows.
- [Engine](engine.md): Core logic and pipelines.
  - [Workflows](../reference/workflows.md): Orchestrations for init, build, watch, test, publish, add-page, add-test.
  - [Workers](engine.md#workers): Per-area units: ClientWorker, ServerWorker, SharedWorker.
  - [Pipelines](pipelines.md): HTML, CSS, JS compilers/bundlers and diagnostics.
  - [Services](services.md): DevService, WatchService, ChangeService.
  - [Servers](servers.md): Static web server (ASP.NET Core) + Node API host.
  - [Templates](../reference/templates.md): Embedded project template (client/server/shared/types).
  - [Workspace](workspace.md): Centralized paths and workspace utilities.
- [Tests](testing.md): .NET test harness validating CLI workflows end-to-end.
- [Sandbox](../how-to/sandbox.md): Docker Compose setup to run a published client (nginx) and the template API server.
- [Utilities](utilities/utilities.md): Repo helper scripts (format, whitespace, build, seed deploy).

## Technology
- Language/Runtime: C# (.NET 9 for CLI), TypeScript for template code.
- Dev Web Server: ASP.NET Core minimal app serves `build/` with SSE for reload.
- API Server: Node.js runs compiled `src/server/index.ts` (via `tsc`).
- Build System: Custom C# pipelines for HTML, CSS, JS.
  - TypeScript: `tsc --build` using embedded `base.tsconfig.json`.
  - CSS: Import resolution, optional CSS Modules in publish, prefixing, minify.
  - JS: ESM-only module graph, tree-shake + minify in publish.
- Change Detection: `FileSystemWatcher` + buffered change queue; restarts API server on server code changes; notifies browser via SSE.
- Caching/URLs: Clean URLs, API proxy (`/api/*` → Node), cache headers for dev/prod.

## How It Works
1. Init (`webstir init [options] [directory]`)
   - Creates a full-stack project (or client/server-only) from embedded templates.
   - Layout: `src/client/app`, `src/client/pages/<page>`, `src/server`, `src/shared`, `types/`.
2. Build (`webstir build`)
   - Compiles TS, processes CSS imports, merges page HTML with `app.html` into `build/`.
3. Watch (`webstir watch`) — default
   - Runs an initial build and tests, starts the web server and Node API server, then watches `src/**`.
   - On change: incrementally rebuilds the affected area, restarts Node for server changes, and signals clients to reload via SSE.
4. Test (`webstir test`)
   - Builds then runs compiled tests with a lightweight Node runner; reports pass/fail summary.
5. Publish (`webstir publish`)
   - Produces optimized assets in `dist/` per page: timestamped `index.<ts>.css/js` written alongside `manifest.json`.
   - HTML is minified and references are rewritten using the per-page manifest; source maps and comments are removed.
6. Generators
   - `webstir add-page <name>` scaffolds `index.html|css|ts` under `src/client/pages/<name>/`.
   - `webstir add-test <name-or-path>` scaffolds a `.test.ts` under the closest `tests/` folder.

## Conventions & Structure
- Base HTML: `src/client/app/app.html` must contain a `<main>`; page fragments merge into it.
- Pages: `src/client/pages/<page>/index.html|css|ts` (publish supports `<page>/index.module.css`).
- App assets: `src/client/app/*` copied as-is (e.g., `refresh.js`).
- Shared types: `src/shared/` (consumed by both client and server).
- Server: `src/server/index.ts` compiled to `build/server/index.js` and run by Node.
- Outputs:
  - Dev: `build/client/**`, `build/server/**` with readable output and refresh support.
- Prod: `dist/client/pages/<page>/index.<timestamp>.{css|js}`, HTML with rewritten links, per-page `manifest.json`.

## CLI Summary
- `init [--client-only|--server-only] [--project-name|-p <name>] [directory]`
- `build [--clean]`
- `watch` (alias: no command)
- `test`
- `publish`
- `add-page <name>`
- `add-test <name-or-path>`
- `help [command]`

See also: [CLI reference](../reference/cli.md)

## Related Docs
- Docs index — [overview](../README.md)
- Templates — [templates](../reference/templates.md)
- Testing — [tests](testing.md) and [.codex/testing.md](../../.codex/testing.md)
- Workspace & paths — [workspace](workspace.md)
- CLI reference — [cli reference](../reference/cli.md)

## Scope & Non-Goals
- Keep defaults simple and predictable; prefer conventions over configuration.
- Avoid complex plugin systems and heavy third-party build chains.
- Focus on end-to-end workflows (init → build → watch/test → publish).

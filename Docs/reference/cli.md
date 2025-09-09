# CLI

Detailed reference for the `webstir` CLI. This expands the high-level overview in [solution.md](../explanations/solution.md) and focuses on concrete behavior, options, and outputs.

## Overview
- Single entrypoint: `webstir <command> [options]`.
- Default command is `watch` when no command is provided.
- Works from the project root created by `init`.
- Orchestrates builds, the dev web server, the Node API server, and generators.

## Usage

```
webstir <command> [options]
webstir help [command]
```

Common patterns:
- `webstir init my-app`
- `webstir init --client-only my-client`
- `webstir build --clean`
- `webstir watch`
- `webstir test`
- `webstir publish`
- `webstir add-page about`
- `webstir add-test auth/login`

## Commands

### init
Usage: `webstir init [--client-only|--server-only] [--project-name|-p <name>] [directory]`

What it does:
- Creates a new project from embedded templates (client, server, shared, and types).
- Layout includes `src/client/app`, `src/client/pages/<page>`, `src/server`, `src/shared`, and `types/`.
- Initializes a working dev setup that builds and runs with `webstir watch`.

Notes:
- Choose one of `--client-only` or `--server-only` to limit what is scaffolded.
- Without options, generates a full-stack project.

### build
Usage: `webstir build [--clean]`

What it does:
- Compiles TypeScript via `tsc --build` using the embedded base config.
- Processes CSS imports; copies app assets.
- Merges page HTML fragments with `src/client/app/app.html` and writes to `build/client/`.
- Compiles server code to `build/server/`.

Notes:
- `--clean` removes previous `build/` before compiling.

### watch (default)
Usage: `webstir watch`

What it does:
- Runs an initial `build` and `test`.
- Starts the dev web server (serves `build/client/**`) and the Node API server (runs `build/server/index.js`).
- Watches `src/**` for changes and performs targeted, incremental rebuilds.
- Restarts the Node server on server changes; notifies browsers via SSE to reload on client changes.

### test
Usage: `webstir test`

What it does:
- Builds the project, then runs compiled tests with a lightweight Node runner.
- Prints a pass/fail summary; integrates with CI using standard exit codes.

### publish
Usage: `webstir publish`

What it does:
- Produces optimized assets in `dist/` for each page.
- Rewrites HTML to reference fingerprinted assets and emits a per-page `manifest.json`.
- Minifies HTML/CSS/JS and removes comments and source maps.

### add-page
Usage: `webstir add-page <name>`

What it does:
- Scaffolds `index.html`, `index.css`, and `index.ts` in `src/client/pages/<name>/`.
- Ready to build and serve without extra configuration.

### add-test
Usage: `webstir add-test <name-or-path>`

What it does:
- Creates a `.test.ts` file under the nearest `tests/` folder relative to the path provided.
- Works for both client and server test locations.

## Dev Server & Watch
- Serves `build/` over ASP.NET Core with an SSE endpoint for live reload.
- Proxies API requests from `/api/*` to the Node server.
- Clean URLs and sensible cache headers for dev vs. prod.
- File watching uses a buffered queue to avoid thrashing on burst changes.

Change impact:
- Client change → rebuild affected page/assets → broadcast reload via SSE.
- Server change → rebuild server → restart Node process.
- Shared change → rebuild both sides that depend on shared modules.

## Build & Publish Pipelines

HTML:
- Page HTML merges into `src/client/app/app.html` (requires a `<main>` element) and is written to `build/client/pages/<page>/index.html` in dev.
- In publish, HTML is minified and rewritten to reference fingerprinted assets listed in a per-page manifest.

CSS:
- Resolves `@import` and URL references; copies assets.
- In publish, supports optional CSS Modules, autoprefixing, and minification.

JavaScript/TypeScript:
- Uses `tsc --build` with an embedded `base.tsconfig.json`.
- ESM-only module graph.
- Tree-shakes and minifies in publish.

Outputs:
- Dev: `build/client/**`, `build/server/**` with readable output and live-reload.
- Prod: `dist/client/pages/<page>/index.<timestamp>.{css|js}`, HTML with rewritten links, and a per-page `manifest.json`.

## Project Layout
- Base HTML: `src/client/app/app.html` contains a `<main>` used for page composition.
- Pages: `src/client/pages/<page>/index.html|css|ts`.
- App assets: `src/client/app/*` (copied as-is to `build/`).
- Shared types/modules: `src/shared/`.
- Server entry: `src/server/index.ts` → `build/server/index.js`.

## Examples
- Create a full-stack app: `webstir init my-app && cd my-app && webstir watch`
- Client-only app: `webstir init --client-only my-client && cd my-client && webstir watch`
- Clean build: `webstir build --clean`
- Add a page: `webstir add-page about && webstir watch`
- Publish for production: `webstir publish`

## Help Output (Sample)
```
$ webstir --help
webstir <command> [options]

Commands:
  init        Create a new project
  build       Build client and server
  watch       Build, run servers, and watch (default)
  test        Build then run tests
  publish     Produce optimized dist outputs
  add-page    Scaffold a new client page
  add-test    Scaffold a new test file
  help        Show help for a command

Options:
  -h, --help     Show help
  -v, --version  Show version

$ webstir help build
Usage: webstir build [--clean]

Builds the client and server into ./build.

Options:
  --clean  Remove the ./build directory before compiling
```

## Exit Codes
- `0`: Success
- Non-zero: Error (build failure, test failure, invalid arguments, or runtime error)

## Contributing Notes
- Commands live in `CLI/`; workflows and services live in `Engine/`.
- `Engine/Workflows/` orchestrates `init`, `build`, `watch`, `test`, `publish`, `add-page`, `add-test`.
- Use `AppWorkspace` and `Engine/Constants.cs` for paths. Avoid manual string manipulation; prefer helpers in `Engine.Extensions`.
- Keep changes minimal and behavior-preserving. Follow `.codex/style.md` and `.editorconfig` when editing C#.

## Related Docs
- High-level solution overview — [solution](../explanations/solution.md)
- Templates — [templates](templates.md)
- Testing — [.codex/testing.md](../../.codex/testing.md), [tests](../explanations/testing.md)
- Workspace and paths — [workspace](../explanations/workspace.md)

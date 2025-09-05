# Templates

Embedded scaffolding used by the CLI to create projects and generate files. Keeps new apps consistent and zero‑config.

## Overview
- Lives inside the engine and is embedded into the CLI.
- `webstir init` lays down a full‑stack project by default.
- Generators add files in the right place with sensible defaults.

## Layout
Created by `webstir init` unless you choose client‑only or server‑only:
- Client: `src/client/`
  - App shell: `src/client/app/app.html`, `src/client/app/*.js|css|png` (copied as‑is)
  - Pages: `src/client/pages/<page>/index.html|css|ts` (seed includes `home`)
- Server: `src/server/index.ts` (Node API server entry)
- Shared: `src/shared/**` (types/modules used on both sides)
- Types: `types/**` (ambient `.d.ts` and tsconfig support)
- Tests: `tests/**` (optional; used by `webstir test`)

## Conventions
- Base HTML requires a `<main>` in `src/client/app/app.html` for page merge.
- Page folder names must be URL‑safe: letters, numbers, `_` and `-`.
- Each page has `index.html`, `index.css`, `index.ts`.
- Server entry is `src/server/index.ts` and must export an HTTP server.

## TypeScript
- Uses an embedded `base.tsconfig.json` referenced by template tsconfigs.
- ESM‑only; compiled via `tsc --build` in dev and publish.
- Shared code in `src/shared` is compiled for both client and server.

## CSS & Assets
- Plain CSS by default; optional CSS Modules in publish.
- `@import` and asset URLs are resolved; files copied to outputs.
- Place static app assets under `src/client/app/*`.

## Generators

### add-page
- Command: `webstir add-page <name>`
- Creates `src/client/pages/<name>/index.html|css|ts` if not present.
- Does not modify existing pages or `app.html`.
- Name normalization: trims, lowercases, replaces spaces with `-`.

### add-test
- Command: `webstir add-test <name-or-path>`
- Creates `<name>.test.ts` under the nearest `tests/` directory.
- Works for both client and server tests.

## Server Template
- Minimal Node server at `src/server/index.ts`.
- Exposes health endpoint (e.g., `GET /api/health`).
- Reads `PORT` env var; defaults handled by the CLI dev server proxy in dev.

## Publish Outputs
- Per page: `dist/client/pages/<page>/index.html`
- Fingerprinted assets: `dist/client/pages/<page>/index.<timestamp>.{css|js}`
- Per‑page `manifest.json` listing hashed asset names.
- App assets copied to `dist/client/app/*`.

## Customizing Templates
- Edit templates under `Engine/Templates/` in the source.
- Keep conventions intact (page structure, base HTML `<main>`, server entry path).
- After changes, rebuild the CLI to embed updated templates.

## Related Docs
- Solution overview — [solution](../solution.md)
- CLI reference — [cli](../cli/cli.md)
- Engine internals — [engine](../engine/engine.md)
- Pipelines — [pipelines](../../pipelines/pipelines.md)
- Workspace and paths — [workspace](../workspace/workspace.md)

# Templates

Embedded scaffolding used by the CLI to create projects and generate files. Keeps new apps consistent and zero-config.

## Overview
- Lives inside the engine and is embedded into the CLI.
- `webstir init` lays down a full-stack project by default.
- Generators add files in the right place with sensible defaults.

## Layout
Created by `webstir init` unless you choose client-only or server-only:
- Frontend: `src/frontend/`
  - App shell: `src/frontend/app/app.html`, `src/frontend/app/*.js|css|png` (copied as-is)
  - Pages: `src/frontend/pages/<page>/index.html|css|ts` (seed includes `home`)
- Backend: `src/backend/index.ts` (Node API server entry)
- Shared: `src/shared/**` (types/modules used on both sides)
- Types: `types/**` (ambient `.d.ts` and tsconfig support)
- Tests: `tests/**` (optional; used by `webstir test`)

## Conventions
- Base HTML requires a `<main>` in `src/frontend/app/app.html` for page merge.
- Page folder names must be URL-safe: letters, numbers, `_` and `-`.
- Each page has `index.html`, `index.css`, `index.ts`.
- Backend entry is `src/backend/index.ts` and must export an HTTP server.

## TypeScript
- Uses an embedded `base.tsconfig.json` referenced by template tsconfigs.
- ESM-only; compiled via `tsc --build` in dev and publish.
- Shared code in `src/shared` is compiled for both frontend and backend.
 - Dev tsconfig enables `sourceMap` and `inlineSources` for smooth debugging; publish never ships source maps and strips any `sourceMappingURL`.

## CSS & Assets
- Plain CSS by default; optional CSS Modules in publish.
- `@import` and asset URLs are resolved; files copied to outputs.
- Place static app assets under `src/frontend/app/*`.
- Place Images, Fonts, and Media under `src/frontend/{images|fonts|media}/**`.

## Client Error Reporting
- The base `app.html` includes `/app/error.js` which installs a lightweight client error handler.
- It listens for `window` `error` and `unhandledrejection` and reports to `POST /client-errors` using `sendBeacon` (fallback to `fetch`).
- Behavior:
  - Throttled: max 1 event/second; capped at 20 per page session.
  - Deduped: repeats suppressed within 60s using a fingerprint of type|message|file:line:col|stack-hash.
  - Correlation: includes a client correlation id; server also accepts `X-Correlation-ID`.
- Override: set `window.__WEBSTIR_ON_ERROR__ = (event) => { /* custom */ }` before errors occur to customize reporting.
- Opt-out: remove the `<script src="/app/error.js" async></script>` tag from your `src/frontend/app/app.html`.

## Generators

### add-page
- Command: `webstir add-page <name>`
- Creates `src/frontend/pages/<name>/index.html|css|ts` if not present.
- Does not modify existing pages or `app.html`.
- Name normalization: trims, lowercases, replaces spaces with `-`.

### add-test
- Command: `webstir add-test <name-or-path>`
- Creates `<name>.test.ts` under the nearest `tests/` directory.
- Works for both frontend and backend tests.

## Backend Template
- Minimal Node server at `src/backend/index.ts`.
- Exposes health endpoint (e.g., `GET /api/health`).
- Reads `PORT` env var; defaults handled by the CLI dev server proxy in dev.

## Publish Outputs
- Per page: `dist/frontend/pages/<page>/index.html`
- Fingerprinted assets: `dist/frontend/pages/<page>/index.<timestamp>.{css|js}`
- Per-page `manifest.json` listing hashed asset names.
- App assets copied to `dist/frontend/app/*`.

## Customizing Templates
- Edit templates under `Engine/Templates/` in the source.
- Keep conventions intact (page structure, base HTML `<main>`, server entry path).
- After changes, rebuild the CLI to embed updated templates.

## Related Docs
- Solution overview — [solution](../explanations/solution.md)
- CLI reference — [cli](cli.md)
- Engine internals — [engine](../explanations/engine.md)
- Pipelines — [pipelines](../explanations/pipelines.md)
- Workspace and paths — [workspace](../explanations/workspace.md)

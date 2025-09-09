# Contracts & Invariants

Public, user-visible guarantees that Webstir preserves across releases. These are validated by end-to-end tests and serve as the stable contract between the CLI and projects.

## Source Layout
- Client: `src/client/**`
- Pages: `src/client/pages/<page>/index.html|css|ts`
- App assets: `src/client/app/**`
- Static assets: `src/client/{images|fonts|media}/**`
- Server: `src/server/**` (entry: `src/server/index.ts`)
- Shared: `src/shared/**`
- Types: `types/**`

## Dev Outputs
- `build/client/**` (includes `pages`, `images`, `fonts`, `media`)
- `build/server/**`

## Publish Outputs
- Per page under `dist/client/pages/<page>/`:
  - `index.html`
  - `index.<timestamp>.css`
  - `index.<timestamp>.js`
  - `manifest.json` (lists fingerprinted asset names)
- Static assets under `dist/client/{images|fonts|media}/**`
- App assets under `dist/client/app/**`

## HTML Composition
- Base HTML at `src/client/app/app.html` must contain a `<main>` where page HTML is merged.
- Clean URLs in dev: `/about` serves `/pages/about/index.html`.

## JavaScript/TypeScript
- ESM-only module graph; CommonJS is not supported.
- Compiled via `tsc --build` using the embedded base `tsconfig`.

## CSS
- Resolves `@import` and asset URLs in dev.
- Minified/autoprefixed in publish; CSS Modules supported in publish.

## Dev Server
- Serves `build/client/**` with SSE reload and an `/api/*` proxy to the Node server.
- No-cache headers for HTML; short/no-cache for static assets in dev.

## Error Handling
- Missing required inputs (base HTML, server entry) fails fast with clear messages.
- Publish removes comments and source maps from outputs.

## CLI Guarantees
- Commands: `init`, `build`, `watch`, `test`, `publish`, `add-page`, `add-test`, `help`.
- Default command: `watch` when no command is provided.


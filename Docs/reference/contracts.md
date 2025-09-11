# Contracts & Invariants

Public, user-visible guarantees that Webstir preserves across releases. These are validated by end-to-end tests and serve as the stable contract between the CLI and projects.

## Source Layout
- Frontend: `src/frontend/**`
- Pages: `src/frontend/pages/<page>/index.html|css|ts`
- App assets: `src/frontend/app/**`
- Static assets: `src/frontend/{images|fonts|media}/**`
- Backend: `src/backend/**` (entry: `src/backend/index.ts`)
- Shared: `src/shared/**`
- Types: `types/**`

## Dev Outputs
- `build/frontend/**` (includes `pages`, `images`, `fonts`, `media`)
- `build/backend/**`

## Publish Outputs
- Per page under `dist/frontend/pages/<page>/`:
  - `index.html`
  - `index.<timestamp>.css`
  - `index.<timestamp>.js`
  - `manifest.json` (lists fingerprinted asset names)
- Static assets under `dist/frontend/{images|fonts|media}/**`
- App assets under `dist/frontend/app/**`

## HTML Composition
- Base HTML at `src/frontend/app/app.html` must contain a `<main>` where page HTML is merged.
- Clean URLs in dev: `/about` serves `/pages/about/index.html`.

## JavaScript/TypeScript
- ESM-only module graph; CommonJS is not supported.
- Compiled via `tsc --build` using the embedded base `tsconfig`.

## CSS
- Resolves `@import` and asset URLs in dev.
- Publish minification/prefixing (modern-only): preserves strings/urls and license comments; trims whitespace and trailing `;`; normalizes numbers/zeros; collapses zero shorthands; shortens hex (`#rrggbb→#rgb`, `#rrggbbaa→#rgba`); strips legacy prefixes/values (`-ms-*`, `-o-*`, `-khtml-*`, legacy flexbox); adds minimal `-webkit-` where needed.

## Dev Server
- Serves `build/frontend/**` with SSE reload and an `/api/*` proxy to the Node server.
- No-cache headers for HTML; short/no-cache for static assets in dev.
- Accepts client error reports at `POST /client-errors`:
  - Requires `Content-Type: application/json` and body <= 32KB.
  - Returns `204` on success; `415` for unsupported media type; `413` if payload too large.
  - Forwards to the error tracking hook with correlation id support (`X-Correlation-ID` or payload `correlationId`).

## Error Handling
- Missing required inputs (base HTML, server entry) fails fast with clear messages.
- Publish removes comments and source maps from outputs.
 - Template includes a client error handler (`/app/error.js`) that throttles to 1/sec (max 20/session) and deduplicates repeats for 60s.

## CLI Guarantees
- Commands: `init`, `build`, `watch`, `test`, `publish`, `add-page`, `add-test`, `help`.
- Default command: `watch` when no command is provided.

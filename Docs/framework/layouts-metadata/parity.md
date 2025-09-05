# Layouts & Metadata

## Current (Webstir)
- Single base template (`app.html`) composed with per‑page fragments.
- Head merging: page `<head>` content injected before `</head>`.
- No nested layouts, partials, or metadata API.

## Baseline (Others)
- Next.js App Router: nested layouts, templates, route metadata API, parallel routes.
- SvelteKit/Remix: `+layout.*` files, `<svelte:head>`/`Meta` APIs, error/loading boundaries.

## Gaps to Parity
- (P1) Per‑folder layouts and partials with deterministic merge order.
- (P2) Route‑level metadata API (title, meta, links) with SSR/SSG integration.
- (P3) Error/loading boundary conventions alongside layouts.

## Parity Plan
- Introduce `pages/<route>/_layout.html` and optional partials (`_head.html`, `_nav.html`, `_footer.html`).
- Define merge rules: nearest layout wins; inherit upwards; page fragment fills `<main>`; page `<head>` augments layout head.
- Add metadata sources: front‑matter in page/layout and an optional `metadata.ts` that exports a typed object.


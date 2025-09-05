# Requirements: Layouts & Metadata

- Scope: nested layouts/partials and a metadata API per route.

## Customer Requirements (CR)
- Compose pages from shared layouts and partials without heavy tooling.
- Define titles/meta/links per route that work with SSR/SSG.
- Predictable merge order when multiple layouts apply.

## Product Requirements (PR)
- Per‑folder layout convention: `pages/<route>/_layout.html`.
- Optional partials: `_head.html`, `_nav.html`, `_footer.html`.
- Metadata sources: front‑matter in page/layout and optional `metadata.ts` export.
- Deterministic merge rules: nearest layout wins; inherit upward; page fills `<main>`.

## Software Requirements (SR)
- Parse and merge layouts with page fragments using defined order.
- Collect metadata and serialize for SSR/SSG and client usage.
- Allow per‑route error/loading boundaries alongside layouts (optional).

## Acceptance Criteria
- A nested page renders with parent and child layouts composed correctly.
- Metadata sets `<title>` and meta tags in SSR and SSG outputs.
- Changing a partial updates all pages that include it in dev.

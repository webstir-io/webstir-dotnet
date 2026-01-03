# Content pipeline

This starter supports Markdown docs out of the box.

If you add files under `src/frontend/content/**/*.md`, Webstir will:

- Convert Markdown to HTML at build time
- Wrap it in your shared layout (`src/frontend/app/app.html`)
- Write real pages under `/docs/.../`

The starter includes a docs hub at `/docs/`.

## Add a doc

Create a file like:

- `src/frontend/content/my-doc.md`

It becomes:

- `/docs/my-doc/`

## Where the output goes

For example, this file:

- `src/frontend/content/content-pipeline.md`

builds to:

- `/docs/content-pipeline/`

## Routing rules (simple)

- In a folder, `index.md` becomes the folder root (e.g. `src/frontend/content/guides/index.md` → `/docs/guides/`)
- In a folder, `readme.md` also becomes the folder root (e.g. `src/frontend/content/guides/readme.md` → `/docs/guides/`)
- Otherwise, the file name becomes a segment (e.g. `content-pipeline.md` → `/docs/content-pipeline/`)

## Frontmatter (optional)

You can set a title/description at the top of a Markdown file:

```md
---
title: My doc
description: A short summary for the docs index.
order: 10
---
```

## Content navigation (optional)

Enable docs navigation UI:

- `webstir enable content-nav`

When enabled, Webstir adds a docs sidebar and breadcrumb:

- Sidebar tree is derived from folders + frontmatter titles (and `order`).
- Breadcrumb labels follow the same frontmatter titles.

## Search (optional)

Enable site-wide search:

- `webstir enable search`

When enabled, Webstir generates search data and adds the search UI assets to your app:

- `/search.json` (used by the search feature module)
- `src/frontend/app/scripts/features/search.ts` (search behavior, added to your source tree and imported by `src/frontend/app/app.ts`)
- `src/frontend/app/styles/features/search.css` (search UI styling, added to your source tree)

## Development vs publish

- `webstir watch` builds to `build/frontend/` for a fast dev loop.
- `webstir publish` writes `dist/frontend/` for static hosting.

## Clean URLs on static hosts

If you deploy `dist/frontend/` with folder-style URLs (like `/docs/content-pipeline/`), configure your host to serve
`index.html` for directory paths.

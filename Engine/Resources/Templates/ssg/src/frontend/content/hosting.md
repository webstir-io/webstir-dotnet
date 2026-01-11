---
title: Deploying your site
description: Clean URLs, sitemap, and static hosting notes for Webstir SSG.
order: 2
---

# Deploying your site

Webstir SSG outputs plain HTML files that can be hosted on any static host (S3, Netlify, Cloudflare Pages, etc.).

## Clean URLs (recommended)

The publish output is directory-based:

- `/about/` → `about/index.html`
- `/docs/content-pipeline/` → `docs/content-pipeline/index.html`

Your host should serve `index.html` automatically when a URL points at a directory.

If you want `/about` (no trailing slash) to work too, configure your host to treat it like a directory request.

Example (nginx):

```nginx
location / {
  try_files $uri $uri/ $uri/index.html =404;
}
```

## GitHub Pages (project sites)

GitHub Pages project sites live under a subpath (for example, `https://user.github.io/repo/`).
Configure a base path so published URLs resolve correctly in SSG output:

- `webstir enable github-pages <repo>`
- `webstir enable gh-deploy <repo>` (also scaffolds a GitHub Actions deploy workflow)
- Or set `frontend.config.json`:

```json
{
  "publish": {
    "basePath": "/repo"
  }
}
```

When enabled, Webstir adds `utils/deploy-gh-pages.sh` and wires it to `npm run deploy`.

When you enable `gh-deploy`, Webstir also adds `.github/workflows/webstir-gh-pages.yml` to deploy automatically on push to `main`.

## Sitemap

Webstir writes `sitemap.xml` during SSG publish. For absolute `<loc>` entries, set:

- `WEBSTIR_SITE_URL=https://your-domain.com`

Or add to `package.json`:

```json
{
  "webstir": {
    "siteUrl": "https://your-domain.com"
  }
}
```

## Broken link checks

During SSG publish, Webstir validates internal links and `#hash` anchors across the generated HTML pages and fails with actionable errors if anything is broken.

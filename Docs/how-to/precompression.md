# Precompressed Assets

Webstir precompresses HTML, CSS, and JS assets at publish as sibling files:

- `.br`: Brotli (quality 11)
- `.gz`: gzip (level ~9)

Artifacts live next to the emitted file in `dist/frontend/pages/<page>/`.

## Why
- Smaller transfer size and faster TTFB without on-the-fly compression.
- Deterministic outputs at publish; better CDN cacheability.

## How to Serve
- Prefer `br`; fall back to `gzip`; otherwise serve the plain file.
- Set headers when serving precompressed variants:
  - `Content-Encoding: br` or `gzip`
  - `Content-Type` appropriate to the asset (e.g., `text/html; charset=utf-8`, `text/css; charset=utf-8`, `application/javascript`)
  - `Vary: Accept-Encoding`

### Example rules (generic)
- If request is for `index.<hash>.<ext>` and client accepts Brotli (`Accept-Encoding: br`), serve `index.<hash>.<ext>.br` with `Content-Encoding: br`.
- Else if client accepts gzip (`Accept-Encoding: gzip`), serve `index.<hash>.<ext>.gz` with `Content-Encoding: gzip`.
- Else serve `index.<hash>.<ext>` without `Content-Encoding`.

## CDN Guidance
- Enable caching for all three variants.
- Ensure cache key varies by `Accept-Encoding` or by the file variant path.
- Avoid edge/on-demand compression for these files; it adds latency and can defeat deterministic caching.

## Notes
- `.html`, `.css`, and `.js` are precompressed. Images, fonts, and videos are already compressed and should not be double-compressed.
- Gzip MTIME is zeroed during publish for stable, repeatable artifacts.

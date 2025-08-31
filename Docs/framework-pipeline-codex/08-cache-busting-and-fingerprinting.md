# Cache Busting and Fingerprinting

## Goal
- Development: avoid caching entirely so changes reflect without hard reloads.
- Production: fingerprint static assets (JS/CSS) so browsers cache them aggressively, while HTML stays un-cached to pick up new asset names automatically.

## Strategy
- Fingerprint filenames at publish time: `index.<fingerprint>.js` and `index.<fingerprint>.css` per page.
- Emit a per-page manifest that records the final filenames so HTML publish can rewrite references.
- Long-cache fingerprinted assets; `no-cache` HTML.

## Fingerprint Options
- `timestamp` (default for simplicity): `index.1725058123.js` (10-digit Unix seconds). Works with the existing server regex for long-cache.
- `contenthash`: `index.a1b2c3d4.js` (8–20 hex chars from SHA-1/SHA-256). Requires a small update to the server’s long-cache pattern.
- `none`: keep `index.js/css` (dev convenience only).

Recommended defaults
- Dev: `none` (headers already disable caching), or optionally leave as `timestamp`.
- Prod: `timestamp` first (zero server change), upgrade to `contenthash` later if desired.

## Manifest
- One manifest per page directory in `dist`:
  - Path: `dist/pages/<page>/manifest.json`
  - Shape:
```
{
  "js": "index.1725058123.js",
  "css": "index.1725058123.css",
  "map": {
    "js": "index.1725058123.js.map",
    "css": "index.1725058123.css.map"
  }
}
```
- If a page lacks JS or CSS, omit that field.

## Integration Points
- JavaScript Publish (Engine/Pipelines/JavaScript/Publish/JsBundler.cs)
  - After building `bundleCode`, compute fingerprint:
    - `timestamp`: `var fp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();`
    - `contenthash`: hash of `bundleCode` (e.g., SHA-256 → first 8–12 hex chars).
  - Write `index.<fp>.js` and optional `index.<fp>.js.map`.
  - Write/merge manifest `{ js, map.js }`.

- CSS Publish (Engine/Pipelines/Css/Publish/CssBundler.cs)
  - After minification, compute fingerprint similarly.
  - Write `index.<fp>.css` and optional `index.<fp>.css.map`.
  - Write/merge manifest `{ css, map.css }`.

- HTML Publish (Engine/Pipelines/Html/Publish/HtmlBundler.cs)
  - For each page, read `manifest.json` if present.
  - Replace references to `index.js`/`index.css` (and source map comments if needed) with the fingerprinted names.
  - Keep HTML headers/cache behavior unchanged (server handles no-cache for HTML).

## Server Headers (Engine/Servers/WebServer.cs)
- Existing behavior:
  - `no-cache` for HTML and dev-only assets.
  - `public, max-age=31536000, immutable` for timestamped assets matching:
    - `\.\d{10}\.(css|js|png|jpg|jpeg|gif|svg|webp|ico)$`
- If switching to `contenthash`, add or adjust a second pattern, e.g.:
  - `\.[a-f0-9]{8,20}\.(css|js|png|jpg|jpeg|gif|svg|webp|ico)$`
- Continue to set `no-cache, must-revalidate` for non-fingerprinted static assets during dev.

## Configuration
- Add a simple option (future) surfaced via CLI/config:
```
CacheBusting: "timestamp" | "contenthash" | "none"
```
- Default by environment: `none` in dev, `timestamp` in prod.

## Edge Cases & Notes
- Source maps: Fingerprint maps to match the asset (e.g., `index.<fp>.js.map`) and update the `//# sourceMappingURL=` comment.
- Cleanup: When `Clean` option is enabled, remove old fingerprinted assets in page folders; otherwise, multiple versions may accumulate.
- Multiple chunks (future): Extend manifest to arrays `{ js: [ ... ], css: [ ... ] }` and support code splitting later.
- HTML fragments with custom names: Prefer swapping only `index.js/css` references; keep advanced rewriting for later.

## Validation Checklist
- Dev server: Always reflects changes without manual cache clearing.
- Prod build: Generates fingerprinted assets and manifest per page.
- HTML publish: Rewrites asset links correctly per page.
- Server: Applies long-cache headers to fingerprinted assets, no-cache to HTML.

## Rollout Plan
1. Implement `timestamp` fingerprints + manifest write in JS/CSS publish.
2. Update HTML publish to consume manifest and swap references.
3. Verify with existing server regex for long-cache (no server change required).
4. Optional: Add `contenthash` and support an additional regex for long-cache.

# Frontend Hardening Checklist

This is a practical pre‑production checklist for Webstir’s frontend pipeline. It focuses on critical fixes and deploy hygiene rather than feature work.

## What Looks Solid
- Build: esbuild with minify, tree‑shaking, hashed filenames.
- HTML: critical CSS inlining, image width/height, lazy loading, prefetch hints.
- Assets: `.br` precompression created for HTML/CSS/JS.
- Security: SRI added for external scripts/styles, safe SVG sanitizer.
- Caching: immutable long‑cache for hashed assets; HTML set to no‑cache.

## Status Update (Implemented)
- Sandbox serves Brotli by default
  - Switched compose image to `fholzer/nginx-brotli:latest`.
  - Enabled `brotli on; brotli_static on;` in `Sandbox/web/nginx.conf`.
  - Verified `Content-Encoding: br` for HTML/CSS/JS.
- CSP tightened in Sandbox
  - Removed `'unsafe-inline'` from `script-src` and set baseline directives.
  - Kept `'unsafe-inline'` in `style-src` to support inlined critical CSS.
- HTTPS-enabled Sandbox
  - Added TLS server on `:8443` with self‑signed certs mounted from `Sandbox/certs/`.
  - Script: `Sandbox/generate-certs.sh` creates `dev.crt`/`dev.key` with SAN for `localhost`, `web`, and `127.0.0.1`.
  - HSTS enabled in HTTPS server only.
  - Auto-cert option: `certs` service builds a tiny mkcert container to generate `dev.crt`/`dev.key` into `Sandbox/certs/` and keeps running; `web` waits for files before starting.
- Permissions-Policy & Isolation
  - Sandbox runs with an expanded deny list (`geolocation, microphone, camera, accelerometer, gyroscope, magnetometer, usb, serial, payment, clipboard-read`) so we can spot breakage early.
  - COOP/COEP/CORP stay on in Sandbox HTTPS only to mimic a locked-down edge.

## Critical Gaps To Fix Before Prod (Zero‑Config Defaults)
- Dev vs prod CSP behavior
  - The dev server middleware uses a permissive CSP for hot reload; production uses the edge server config. Keep dev permissive, ship stricter defaults at the edge.
- Tighten CSP by default at the edge
  - Remove `'unsafe-inline'` from `script-src` in the sample Nginx config while keeping `'unsafe-inline'` in `style-src` to support inlined critical CSS.
  - Baseline policy:
    - `default-src 'self'`;
    - `script-src 'self' https:`;
    - `style-src 'self' https: 'unsafe-inline'`;
    - `img-src 'self' https: data:`;
    - `font-src 'self' https: data:`;
    - `connect-src 'self' https:`;
    - `object-src 'none'`; `base-uri 'self'`; `frame-ancestors 'self'`.
- Compression alignment without knobs
  - Sandbox: done (brotli enabled).
  - Production: adopt brotli_static at the edge or ensure CDN serves `.br` variants.
- Permissions-Policy defaults
  - Ship the minimal deny list (geolocation/microphone/camera). Mention Sandbox’s broader deny list as an optional hardening profile.
- COOP/COEP/CORP
  - Leave off by default (compat risk). Document that Sandbox enables them for testing and provide opt-in steps for production.
- Dev API proxy
  - Keep proxy middleware for dev only; do not include it in the production deployment guide.
- Robots in staging
  - Keep allow‑all by default. Provide a staging recipe in docs to serve `Disallow: /` at the edge without app config.

## Concrete Actions (No Config Needed)
- Brotli in Sandbox (complete)
  - Compose now uses brotli-enabled Nginx and `brotli_static` is on. No app changes needed.
- HSTS guidance (edge only)
  - Document adding HSTS in the HTTPS server block (not added in the HTTP sample to avoid confusion).
- Staging robots recipe
  - Document an edge‑only snippet to serve `Disallow: /` for non‑prod hosts (no app changes).

## Nice To Have (Future, Still Zero‑Config)
- CSP nonces/hashes
  - If we later drop inline critical CSS, auto‑generate nonces/hashes for any remaining inline content.
- Source map handling
  - Keep production sourcemaps disabled and `.map` blocked at edge by default.
- Expand SRI coverage
  - Prefer vendoring critical third‑party assets locally to avoid build‑time fetch and ease CSP.

## Quick Verification
- Headers (dev server)
  - `curl -sI http://localhost:8088 | sed -n '1,200p'`
  - Check CSP, HSTS (prod only), Referrer‑Policy, Permissions‑Policy, COOP/COEP/CORP.
- Headers (Nginx)
  - `curl -skI https://localhost:8443 | sed -n '1,200p'`
- Isolation & Permissions
  - `curl -skI https://localhost:8443 | rg -i 'permissions-policy|cross-origin-(opener|embedder|resource)-policy'`
- Brotli served
  - `curl -skI -H 'Accept-Encoding: br' https://localhost:8443/pages/home/index.css | rg -i 'content-encoding: br'`
- Caching
  - Hashed asset: expect `Cache-Control: public, max-age=31536000, immutable`.
  - HTML: expect `no-cache, no-store, must-revalidate`.

## Notes (File Pointers)
- Security headers: `Engine/Middleware/SecurityHeadersMiddleware.cs:16`, `:19–25`
- CSP builder: `Engine/Pipelines/Core/Utilities/ContentSecurityPolicy.cs:7`
- Precompression middleware: `Engine/Middleware/PrecompressionMiddleware.cs:63–76`
- Nginx config: `Sandbox/web/nginx.conf` (brotli/static assets, security headers)
- Sandbox compose: `Sandbox/docker-compose.yml` (brotli-enabled image)
- API proxy (dev only): `Engine/Middleware/ApiProxyMiddleware.cs:16–26`

---

If you want, I can apply the Nginx CSP tweak and enable brotli in the sample config now — still zero config on the app side.

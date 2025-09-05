# Requirements: Output & Packaging

- Scope: content hashing, manifest, sourcemaps, preload/prefetch hints.

## Customer Requirements (CR)
- Deterministic, cache‑friendly filenames for all emitted assets.
- A manifest that tools and servers can consume easily.
- Configurable sourcemaps and license comment handling for production.
- Ability to hint critical resources for faster page load.

## Product Requirements (PR)
- Content hashing for JS/CSS/assets and a manifest mapping logical to hashed paths.
- Production sourcemaps and license comment preservation per policy.
- Preload/prefetch hints for critical chunks and CSS when enabled.

## Software Requirements (SR)
- Hash filenames deterministically based on content; support stable chunk naming.
- Emit `manifest.json` mapping original → hashed URLs; include integrity if enabled.
- Sourcemaps: inline or external per config; exclude inlined sources when configured.
- Preserve license comments in production.
- Generate `<link rel="preload"/>` and `<link rel="prefetch"/>` hints for critical assets when configured.

## Acceptance Criteria
- Rebuilding without changes produces identical hashed filenames and manifest.
- Manifest contains correct original → hashed mappings; consumers can resolve assets.
- Production builds include sourcemaps and preserve license banners per settings.
- When hints are on, HTML includes preload/prefetch entries for configured assets.

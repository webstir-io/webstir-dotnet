# Caching & Performance

## Current (Webstir)
- No persistent transform cache; relies on `tsc` incremental internally.
- Rebuild strategy: page‑level; no per‑module incremental bundling.
- Parallelization limited; most steps run sequentially.

## Baseline (Others)
- Webpack/Rspack: filesystem cache, module cache, loader cache; parallelizable; snapshot invalidation.
- Rollup/Vite: persistent cache (plugins), dependency pre‑bundle cache; fast dev server graph.
- esbuild: internal caching + parallelization; incremental rebuild API.
- Turbopack: fine‑grained incremental caching, on‑demand compilation.

## Gaps to Parity
- (P3) Persistent cache for parse/transform/minify across dev/build.
- (P3) Graph‑aware invalidation to avoid full page rebuilds.
- (P3) Parallel module processing and IO scheduling.
- (P4) Dependency pre‑bundle cache for dev.

## Parity Plan
- Add on‑disk cache keyed by file path + content hash for: JS parse, transformed JS/CSS, minify.
- Track module timestamps/hashes to only re‑emit changed modules and affected dependents.
- Use task parallelism for per‑module work (bounded concurrency).
- Pre‑bundle vendor dependencies once and cache between runs.

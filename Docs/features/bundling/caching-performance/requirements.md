# Requirements: Caching & Performance

- Scope: persistent transform cache, graph‑aware invalidation, parallelization, and dev pre‑bundle cache.
- Goal: fast feedback in dev, reproducible builds, zero‑config defaults, minimal deps.

## Customer Requirements (CR)
- Fast edits: quick rebuilds and HMR in dev for medium projects.
- Short cold starts after the first run.
- Deterministic production builds; caches never change outputs.
- Zero‑config: sensible defaults; advanced controls are optional.
- Minimal disk use and easy cleanup.
- Works locally and in CI across platforms.
- Clear controls to disable or clean caches when needed.

## Product Requirements (PR)
- Persistent cache for parse/transform/minify across dev and build.
- Graph‑aware invalidation: rebuild the changed modules and their dependents only.
- Parallel processing of independent modules with bounded concurrency.
- Dev dependency pre‑bundle with a cache that survives restarts.
- Safe cache keys that include file content, config/options, environment, and tool versions.
- Default cache location within the workspace; configurable via CLI and API.
- Controls: `--no-cache`, `--force`, and a `clean` command/API to remove caches.
- Optional stats: log cache hit/miss and timing summaries in verbose/debug modes.
- No background daemons; all work happens within the dev/build lifecycle.

## Software Requirements (SR)
- Compose cache keys from: file content hash + transform pipeline/options (targets, minify, defines) + tool/plugin versions + mode (dev/prod) + platform/target.
- Store transformed outputs (code + sourcemap) per module; avoid persisting large ASTs by default.
- Maintain a module graph (adjacency) to calculate affected dependents on change; persist lightweight graph metadata.
- Invalidation: when a file or relevant option changes, mark the node dirty and rebuild the impacted subgraph only.
- Concurrency: use a worker pool sized to logical cores (with a reasonable default cap) and allow override.
- Dev pre‑bundle cache: key by lockfile hash (npm/yarn/pnpm) + relevant env/targets; rebuild only when this key changes.
- Cache retention: configurable size limit and strategy (e.g., LRU) with safe defaults.
- File watching: coalesce rapid changes; schedule work to minimize thrash and IO contention.
- Diagnostics: optional debug logs explaining cache misses and invalidation reasons.

## Acceptance Criteria
- Warm dev start: after an initial run, restart of `webstir dev` avoids vendor pre‑bundle and uses transform cache (observable in logs).
- Edit rebuild scope: editing a leaf module rebuilds that module and its dependents only; unrelated modules are not re‑emitted.
- Parallel speedup: on multi‑core machines, a rebuild touching many modules is faster than a single‑thread baseline.
- Deterministic outputs: a production build with `--no-cache` produces the same files as with cache enabled.
- Pre‑bundle invalidation: changing the lockfile (adding/upgrading a dep) triggers a vendor re‑bundle; otherwise it is reused.
- Controls: `--no-cache` disables cache reads/writes; `webstir clean` removes the cache directory and logs the action.

# Bundling

Hub for bundling/build features and the dev server. This doc describes the feature set, indexes requirements by sub‑area, and tracks a prioritized backlog.

Principles
- Focus on outcomes, not tools. Prefer platform features over framework‑specific solutions.
- Minimal dependencies. No PostCSS or Sass; avoid heavy runtimes unless strictly necessary.
- Zero‑config defaults with opt‑in advanced behavior.

## Guardrails
- Platform‑first: ESM‑only, import maps, native browser APIs; avoid framework‑specific runtimes.
- Minimal deps: prefer zero; otherwise lightweight, optional, and replaceable.
- CSS policy: native CSS (nesting, @layer); no PostCSS/Sass by default.
- Zero‑config: works out of the box; advanced behavior is opt‑in.
- HMR scope: ESM/CSS only; framework‑agnostic with optional bridges.
- TypeScript compile gates the pipeline in watch/build/publish/CI; no ESLint by default.
- Determinism: content‑hashed filenames and a manifest for stable outputs.
- Security: explicit env defines; no network during build by default; avoid executing untrusted project code.
- Performance: incremental work, local caches; no background daemons.
- Extensibility: hooks/plugins are optional and off by default; keep the surface small.

## Requirements Index
- Graph & Resolution — [./graph-resolution/requirements.md](./graph-resolution/requirements.md)
- Transforms & Assets — [./transforms-assets/requirements.md](./transforms-assets/requirements.md)
- Dev Server & DX — [./dev-server-dx/requirements.md](./dev-server-dx/requirements.md)
- Output & Packaging — [./output-packaging/requirements.md](./output-packaging/requirements.md)
- Optimization — [./optimization/requirements.md](./optimization/requirements.md)
- Caching & Performance — [./caching-performance/requirements.md](./caching-performance/requirements.md)
- Config & Extensibility — [./config-extensibility/requirements.md](./config-extensibility/requirements.md)
- Diagnostics & Analysis — [./diagnostics-analysis/requirements.md](./diagnostics-analysis/requirements.md)
- Platform & Advanced — [./platform-advanced/requirements.md](./platform-advanced/requirements.md)

## Prioritized Backlog
1. Exports/conditions resolution (package exports/imports maps and env conditions) — [Graph & Resolution](./graph-resolution/requirements.md)
2. Aliases & TS paths (resolve aliases and tsconfig paths; custom hooks) — [Graph & Resolution](./graph-resolution/requirements.md)
3. Production JS transform (TS/JSX, targets, minify, sourcemaps) — [Transforms & Assets](./transforms-assets/requirements.md)
4. Defines/macros with dead-branch elimination (`process.env`, feature flags) — [Transforms & Assets](./transforms-assets/requirements.md)
5. HMR for ESM/CSS (framework‑agnostic) — [Dev Server & DX](./dev-server-dx/requirements.md)
6. Content hashing & manifest (stable hashed JS/CSS/assets) — [Output & Packaging](./output-packaging/requirements.md)
7. Code splitting + runtime loader (dynamic imports, shared/runtime chunks) — [Optimization](./optimization/requirements.md)
8. Error overlay (in-browser overlay with code frames/links) — [Dev Server & DX](./dev-server-dx/requirements.md)
9. JSON/text/asset loaders (url/file/inline with size limits) — [Transforms & Assets](./transforms-assets/requirements.md)
10. Robust tree shaking (sideEffects, PURE annotations, DCE across modules) — [Optimization](./optimization/requirements.md)
11. Production sourcemaps & license comment preservation — [Output & Packaging](./output-packaging/requirements.md)
12. Persistent transform cache (parse/transform/minify across runs) — [Caching & Performance](./caching-performance/requirements.md)
13. Graph-aware invalidation (rebuild changed modules + dependents) — [Caching & Performance](./caching-performance/requirements.md)
14. Parallelization (multi-thread parse/transform/minify; bounded concurrency) — [Caching & Performance](./caching-performance/requirements.md)
15. Dependency pre-bundle for dev (flatten vendors for cold start) — [Optimization](./optimization/requirements.md)
16. Pre-bundle cache persisted between dev runs — [Caching & Performance](./caching-performance/requirements.md)
17. Config file `webstir.config.*` (entries, aliases, externals, defines, targets, maps, splitting) — [Config & Extensibility](./config-extensibility/requirements.md)
18. Plugin system (resolve/load/transform/generate/write hooks) — [Config & Extensibility](./config-extensibility/requirements.md)
19. Externals control (package/pattern externalization) — [Graph & Resolution](./graph-resolution/requirements.md)
20. Asset URL pipeline (consistent URL rewriting + hashing across JS/CSS/HTML) — [Transforms & Assets](./transforms-assets/requirements.md)
21. Native CSS features (nesting, @layer; no PostCSS/Sass) — [Transforms & Assets](./transforms-assets/requirements.md)
22. HTTPS & proxy improvements (advanced rules, CORS helpers) — [Dev Server & DX](./dev-server-dx/requirements.md)
23. Symlink/monorepo workspace handling (hoisting, correct symlink resolution) — [Graph & Resolution](./graph-resolution/requirements.md)
24. Preload/prefetch hints for critical chunks and CSS — [Output & Packaging](./output-packaging/requirements.md)
25. Analyzer/stats report (sizes, duplicates, HTML report) — [Diagnostics & Analysis](./diagnostics-analysis/requirements.md)
26. TypeScript compile gate (always on) — [Diagnostics & Analysis](./diagnostics-analysis/requirements.md)
27. License extraction/report and banner preservation — [Diagnostics & Analysis](./diagnostics-analysis/requirements.md)
28. Integrity hashes + gzip/brotli size reporting — [Diagnostics & Analysis](./diagnostics-analysis/requirements.md)
29. Multi-target builds (web/node/worker) with mode overrides — [Config & Extensibility](./config-extensibility/requirements.md)
30. JS API & richer CLI flags/env — [Config & Extensibility](./config-extensibility/requirements.md)
31. Resolver plugins & virtual modules support — [Graph & Resolution](./graph-resolution/requirements.md)
32. Plug’n’Play compatibility — [Graph & Resolution](./graph-resolution/requirements.md)
33. Workers/service worker bundling + registration helpers — [Platform & Advanced](./platform-advanced/requirements.md)
34. SSR/SSG pipeline (server bundle + hydration maps) — [Platform & Advanced](./platform-advanced/requirements.md)
35. WASM pipeline (copy/inline + async loader glue) — [Platform & Advanced](./platform-advanced/requirements.md)
36. Polyfills per target (inject or document strategy) — [Platform & Advanced](./platform-advanced/requirements.md)
37. Remote modules via ESM import maps (optional) — [Platform & Advanced](./platform-advanced/requirements.md)

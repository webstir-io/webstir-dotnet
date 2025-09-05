# Graph & Resolution

## Current (Webstir)
- JS graph: builds from per‑page entries; tracks deps and cycles (`Engine/Pipelines/JavaScript/Publish/JsModuleGraph.cs`).
- Resolver: relative/absolute (from `src/`), bare specifiers via `node_modules`; supports `main`/`module` fields; `.ts/.js/.mjs/.json` (`Engine/Pipelines/JavaScript/Publish/JsModuleResolver.cs`).
- CSS paths: small namespace map (`@app`, `@components`, `@shared`, `@pages`) and relative resolution (`Engine/Pipelines/Css/Build/CssPathResolver.cs`).
- Externals, alias, TS paths, `exports`/conditions, and virtual modules: not implemented.

## Baseline (Others)
- Webpack/Rspack: advanced resolver (aliases, TS paths, `exports`/conditions, `browser` field, sideEffects, externals, symlinks/PnP).
- Rollup/Vite: ESM‑first graph; `@rollup/plugin-node-resolve` + alias; externals; virtual modules via plugins.
- esbuild: fast resolver; alias/TS paths; platform conditions; externalization flags; virtual modules.
- Turbopack: incremental graph; advanced resolution; focused on Next.js use cases.

## Gaps to Parity
- (P1) Package `exports`/`imports` maps and condition resolution (`import`/`require`, `browser`, `development`/`production`).
- (P1) Aliases and TS path mapping; custom resolve hooks.
- (P4) Externals (package or pattern based); sideEffects from `package.json`.
- (P5) Symlink handling/monorepo workspaces.
- (P7) Plug'n'Play compatibility.
- (P7) Virtual module support and resolver plugin hooks.

## Parity Plan
- Add resolve options (alias, conditions, externals) to `AppSettings` and pass through.
- Extend resolver to honor `exports`/`imports`/`browser` fields and condition sets.
- Implement TS path/alias resolution (read `tsconfig.json` paths).
- Add externals and sideEffects handling in graph and bundler.
- Introduce resolver hooks for plugins and virtual IDs.
- Add tests covering mixed ESM/CJS, exports maps, alias, and externals.

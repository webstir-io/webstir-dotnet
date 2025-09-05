# Config & Extensibility

## Current (Webstir)
- CLI workflows (init/build/watch/publish/test); minimal settings in `AppSettings` (ports/URLs).
- No bundler config file; no plugin system.
- Some path helpers/constants in `Engine/Constants.cs` and `Engine/AppWorkspace.cs`.

## Baseline (Others)
- Webpack/Rspack: rich config, modes, target, plugins/loaders; programmatic API.
- Rollup/Vite: JS config, plugins, per‑target outputs, env modes.
- esbuild: JS API/CLI options; plugins; define/inject; multiple entry/outputs.
- Turbopack: framework‑driven config; experimental plugin hooks.

## Gaps to Parity
- (P4) Config file for bundling (entries, aliases, externals, defines, targets, sourcemaps, splitting).
- (P4) Plugin system with lifecycle hooks (resolve/load/transform/generate/write).
- (P6) Multi‑target builds (web/node/worker) and env‑specific overrides.
- (P6) CLI flags/env handling and JS API for embedding.

## Parity Plan
- Introduce `webstir.config.(json|ts)` with typed schema; load/merge with defaults.
- Implement a lightweight plugin hook bus and expose core hooks.
- Add per‑target config blocks and mode overrides.
- Expand CLI to surface key options and export a small JS API.

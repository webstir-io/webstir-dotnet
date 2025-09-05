# Requirements: Config & Extensibility

- Scope: config file and flags/env, plugin system, externals, multi‑target builds, JS API.

## Customer Requirements (CR)
- Zero‑config defaults with an obvious way to opt into advanced behavior.
- A single config file to control entries, aliases, externals, defines, targets, maps, splitting.
- Extend behavior with small, composable plugins without heavy dependencies.
- Use the tool from both CLI and JS with the same options.

## Product Requirements (PR)
- Config file `webstir.config.*` with typed options and per‑target overrides.
- Plugin system with narrow hooks: resolve, load, transform, generate, write.
- Externals control by package/pattern.
- Multi‑target builds (web/node/worker) with mode overrides.
- JS API and richer CLI flags/env; consistent option names and precedence.

## Software Requirements (SR)
- Discover and load `webstir.config.ts/js/mjs/cjs` and merge with CLI/env; define clear precedence.
- Normalize options and validate with helpful errors.
- Plugin runner with ordered hooks and error isolation.
- Externals: filter modules during resolve/load and adjust outputs accordingly.
- Multi‑target: run separate pipelines per target with shared caches when safe.
- JS API exposes build/dev functions; CLI maps flags/env to the same schema.

## Acceptance Criteria
- Adding `webstir.config.*` can override entry and aliases; builds reflect changes.
- A sample plugin modifies a module during `transform` and its effect shows in output.
- Marking a package external excludes it from bundles.
- A multi‑target build emits distinct outputs for `web` and `node` with target‑specific settings.

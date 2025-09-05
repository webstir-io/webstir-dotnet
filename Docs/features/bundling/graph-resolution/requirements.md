# Requirements: Graph & Resolution

- Scope: build the module graph and resolve imports (package exports/imports with conditions, aliases, TS paths, externals, symlinks/monorepos, Plug’n’Play).

## Customer Requirements (CR)
- Imports “just work” for modern packages that use `exports` and conditions.
- Project aliases and TS `paths` resolve without extra build config.
- Ability to mark packages as external on demand.
- Works in monorepos and with symlinked workspaces.
- Optional compatibility for Plug’n’Play installs.

## Product Requirements (PR)
- Resolve package `exports`/`imports` with Node/WHATWG conditions based on mode/target.
- Support project aliases and TS `paths` mappings.
- Allow externals by package name or pattern.
- Handle symlinks/monorepos correctly; predictable realpath behavior.
- Optional: resolver plugins and virtual modules support.

## Software Requirements (SR)
- Implement ESM resolver that reads `package.json` `exports`/`imports`; apply conditions from config (e.g., `development`, `production`, `browser`, `node`).
- Apply alias and TS `paths` before fallback resolution; ensure stable resolution order.
- Externals: skip bundling and rewrite imports to runtime URLs or leave bare, per target.
- Symlinks: detect workspaces; prefer workspace sources with correct realpath decisions.
- Optional PnP: when detected, use its API to resolve modules; otherwise continue normally.

## Acceptance Criteria
- Popular packages using `exports` resolve in dev and build without extra config.
- TS `paths` mappings work in both dev and build.
- Marking a package external keeps it out of the bundle and leaves imports as configured.
- In a workspace with symlinks, local packages resolve to sources once without duplication.

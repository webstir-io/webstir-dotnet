# Framework Frontend ESM Migration Plan

## Goals
- Raise the minimum supported runtime to Node.js 20.18+ across CLI, tests, and embedded tarballs.
- Publish the TypeScript frontend package as native ESM (with dual exports only if needed).
- Remove legacy CommonJS shims so downstream consumers interact with modern module syntax by default.

## Scope
The plan covers the TypeScript frontend workspace (`framework/frontend`), the .NET bridge that shells into the CLI, and supporting developer tooling. Backend services and templates stay out of scope unless they depend on the frontend package entry points.

## Workstreams

### Runtime & Toolchain
- Update `package.json` engines to `>=20` and audit all scripts for compatibility with Node 20 features (test runner, fetch, permissions).
- Switch `tsconfig.json` to `module: "nodenext"` and `moduleResolution: "nodenext"`; enable `target` upgrades if required by dependencies.
- Regenerate compiled output as `.mjs` (or ESM `.js` under `type: module`) and ensure source maps still generate as expected.

### Package Exports & Bundling
- Add a `package.json` `type: "module"` declaration and expose explicit `exports` for both runtime entry points (`./core`, `./builders`, `./cli`).
- Provide CommonJS fallbacks only if consumers still require them; otherwise document the new ESM-only contract.
- Verify embedded tarball build in the .NET packaging flow uses the new layout and omits obsolete `.cjs` artifacts.

### CLI & Process Invocation
- Convert `dist/cli.js` to ESM-friendly bootstrap (use `import` statements, top-level await where helpful).
- Update `.bin/webstir-frontend` shim if needed to support ESM entry points.
- Adjust `FrontendWorker` and any other .NET orchestration to invoke `node` with the correct flags (`--experimental-loader` not required if package declares ESM correctly) and to honor the new Node 20 requirement.

### Test & Dev Tooling
- Migrate existing Node-based tests to ESM (use `import`/`export` or top-level async); ensure `node --test` runs without `require`.
- Update `utilities/format-build.sh` and CI scripts to use Node 20 tooling (consider `.nvmrc` or Volta pinning).
- Ensure developer docs explain how to run the ESM-based CLI locally, including Node version managers.

### Dependency Audit
- Confirmed dependency compatibility (2025-09-22):
  - `sharp@0.33.5` – CommonJS entry with TypeScript types; supports Node 18.17+ / 20.3+.
  - `glob@10.4.5` – Dual ESM/CJS build; no stricter engine requirement than Node 18.
  - `commander@12.1.0` – CommonJS with ESM stub; Node 18+.
  - `cheerio@1.1.2` – Exposes ESM/CJS; requires Node 20.18.1 which sets our new floor.
  - Remaining runtime deps (`autoprefixer`, `postcss`, `csso`, `fs-extra`, `esbuild@0.25.10`, `zod`) either ship dual mode or CJS with type definitions; all operate under Node 20.18+ via `esModuleInterop`.
- No shims required—the existing import style continues to work under `moduleResolution: "nodenext"`.

## Deliverables
1. Updated `framework/frontend` package that ships as ESM, with documentation and release notes.
2. .NET bridge changes that enforce Node 20, pass through environment detection, and gracefully fail when older runtimes are detected.
3. Migration notes for internal teams detailing how to consume the new ESM artifacts and adjust custom scripts.

## Risks & Mitigations
- **Dependency gaps:** Some libraries may still rely on CommonJS. Mitigate by selecting ESM-compatible alternatives or bundling small shims.
- **Tooling drift:** Local developer environments might still default to Node 18. Provide automation (`.tool-versions`, `setup-node@v4`) to pin versions and surface clear errors.
- **CLI consumers:** Future consumers might still expect CommonJS. Consider providing a minimal `exports["."] = { import, require }` map only if requested; default to ESM to keep maintenance lean.

## Success Criteria
- New workspaces build and publish using Node 20 without additional flags.
- `npm test` and `./utilities/format-build.sh` succeed on clean machines with only Node 20 installed.
- The frontend package tarball contains only ESM entry points, and the .NET worker successfully invokes the CLI in watch/build/publish flows.

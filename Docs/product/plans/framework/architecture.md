# Webstir TypeScript Framework Architecture

## Purpose
Provide a coherent TypeScript-first infrastructure for all generated projects and engine-side processing. The C# CLI will continue to orchestrate workflows, but build/test/publish logic and supporting utilities will live in a structured TypeScript framework.

## Guiding Principles
- **Orchestration vs. Execution:** C# handles command parsing, workspace setup, and process supervision. TypeScript packages handle actual compilation, bundling, testing, and runtime helpers.
- **Package Everything:** All JS/TS assets (test runner, HTML processors, backend utilities) ship as compiled npm packages rather than loose embedded files.
- **Single Source of Truth:** Shared constants, configuration schemas, and entry points defined in TypeScript and consumed by the CLI through generated manifest files.
- **Incremental Migration:** Gradually replace existing embedded JS/C# pipelines with TypeScript equivalents while maintaining compatibility during transition.
- **Developer Experience:** Generated projects look and behave like standard Node-based apps; commands map directly to npm scripts for local iteration.
- **Zero-Config First:** Every workspace runs end-to-end immediately after `init`; any third-party tooling is wrapped with sane defaults and only exposes configuration when explicitly enabled.
- **Controlled Dependencies:** External packages are pinned, audited, and wrapped to maintain predictable builds without leaking configuration complexity to developers.

## Target Architecture
```
webstir/
├── CLI (C#)
│   ├── commands: init/build/watch/publish/test/add-page/add-test
│   └── bridges: shell out to TypeScript CLI tools
├── framework/
│   ├── frontend/
│   │   ├── commands: build, publish, rebuild-on-change
│   │   ├── html/css/js pipelines
│   │   └── shared config manifest
│   ├── backend/ (ts) [future]
│   │   ├── API build/publish routines
│   │   └── dev server plugins
│   ├── testing/
│   │   ├── node test runner
│   │   └── @types exports for helpers
│   └── templates/
│       ├── base project assets
│       └── generators (page/test/etc.)
└── docs/
```

## Phase 1 – Testing Module
1. **Project Setup**
   - Create `framework/testing` TypeScript project with build output (`dist/`).
   - Move current `tester.js` logic into TypeScript; expose CLI entry in `package.json`.
   - Include ambient type definitions in `src/types.d.ts`; emit under `dist/types`.
2. **Distribution Strategy**
   - During CLI build, run `pnpm/npm/yarn` to produce `tgz` artifact embedded into the C# binary or copied into resources.
   - Init workflow extracts the package into generated project (`node_modules` via `npm install <path-to-tgz>` or adds file dependency to package.json).
3. **CLI Integration**
   - `add-test` writes `import` statements pointing to the published package.
   - Tests command shells out: `node ./node_modules/.bin/webstir-test`.
4. **Cleanup**
   - Remove `TestTypeRegistry` and `types/@webstir/**` from engine resources.
   - Update docs to explain npm dependency.

## Phase 2 – Frontend Module
1. **Frontend Framework Module**
   - Reimplement HTML builder, CSS optimizer, JS bundler wrappers in TypeScript.
   - Provide CLI commands: `webstir-frontend build`, `webstir-frontend publish`, and a `rebuild` entry the .NET dev server can call when files change.
   - Emit manifest JSON (paths, settings) consumed by the .NET CLI to pipe logs and status, while the existing dotnet dev server continues to orchestrate watching/serving.
2. **CLI Bridge**
   - Replace `FrontendWorker`, `BackendWorker`, etc., with thin wrappers that invoke `node webstir-build <command>`.
   - Translate exit codes/errors into CLI diagnostics.
3. **Configuration Sync**
   - Define shared config (ports, directories, features) in `framework/frontend/config.ts` and generate a JSON artifact consumed by the CLI.

## Phase 3 – Backend & Templates
1. **Backend Module (`framework/backend`)**
   - Provide dev server, API scaffolding, and publish routines for Node backend.
   - Align with future backend roadmap (proxy, environment config, etc.).
2. **Templates Module (`framework/templates`)**
   - Centralize page/test/backend generators with reusable utilities.
   - Expose TypeScript APIs consumed by the CLI for scaffolding commands.
   - Migrate existing `Engine/Templates/**` assets into the framework-managed catalog.
3. **Unified Scripts**
   - Template package.json exposes scripts: `npm run webstir:build`, `webstir:publish`, `webstir:test` mapping to package CLIs.
   - CLI runs the same scripts to keep parity with developer workflows.
4. **Extensibility**
   - Allow users to override/extend the TypeScript packages via configuration (e.g., custom Babel plugins, additional pipelines).
   - Document hooking points and configuration files.

## Implementation Notes
- There are no existing external projects to migrate, so we can replace the old pipelines outright.
- `init` should generate fresh lockfiles after wiring local framework packages to keep installs deterministic.
- Embed TypeScript module builds (`pnpm build`) into the CLI packaging pipeline; store artifacts alongside other engine resources for offline installs.
- Extend CI to run both .NET tests and the framework’s TypeScript test suites to keep orchestration and execution layers verified end-to-end.

## Next Steps (Initial Execution)
1. Scaffold `framework/testing` with a TypeScript build pipeline.
2. Update CLI build process to compile framework modules before embedding.
3. Modify init/add-test workflows to install/use the testing package.
4. Remove `TestTypeRegistry` and update tests/docs accordingly.
5. Add CI jobs to execute framework unit tests and linting alongside existing .NET suites.

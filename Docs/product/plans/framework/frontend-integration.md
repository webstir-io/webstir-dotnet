# Frontend Module Integration Plan

## Goals
- Replace the existing `FrontendWorker` pipelines with a thin bridge that shells to `webstir-frontend`.
- Keep package management aligned with the testing package (`TestPackageInstaller`).
- Ensure the shared manifest travels from TypeScript → .NET without hardcoded paths.

- ## Status Snapshot
- The TypeScript frontend pipeline now produces dev + publish artifacts with hashed bundles, manifests, and SRI-secured external assets.
- Incremental rebuilds now isolate per-page work, publish emits WebP/AVIF assets with intrinsic dimensions, HTML gains prefetch/lazy-loading/critical CSS inlining, and SRI/resource-hint failures surface as structured CLI warnings.
- The .NET `FrontendWorker` shells out to `webstir-frontend` for build/publish/add-page, reusing embedded npm tarballs and relaying CLI output through structured logging. `add-test` now delegates to the testing CLI for scaffolding.
- Next milestone: swap the C# worker to invoke the new CLI and remove legacy handler dependencies.

## Tasks
1. **Package Embedding**
   - Bundle the `@webstir/frontend` tarball during CLI packaging (same flow as `@webstir/test`).
   - Add an installer helper (`FrontendPackageInstaller`) responsible for copying the tarball and patching `package.json` dependencies.
2. **Worker Bridge**
   - Trim `FrontendWorker` down to:
     - Call `FrontendPackageInstaller.EnsureAsync`.
     - Resolve command arguments (`build`, `publish`, `rebuild`) based on the workflow entry point.
     - Invoke `node ./node_modules/.bin/webstir-frontend <command>` with the workspace root and optional changed file path.
   - Translate process exit codes and stdio into structured `ILogger` messages; treat non-zero exit codes as failures.
3. **Manifest Consumption**
   - Read `.tools/frontend-manifest.json` after each invocation to keep path constants synchronized.
   - Surface manifest data to downstream services (dev server, publishers) instead of recomputing `AppWorkspace` paths.
4. **Telemetry & Diagnostics**
   - Pipe CLI stdout/stderr back into `ILogger` with prefixes (e.g., `[frontend]`).
   - Preserve TypeScript stack traces and hint users when dependencies are stale (`npm install`).

## Testing Checklist
- `dotnet run --project Tests -- test build` (ensures CLI build workflow).
- Manual smoke: invoke `webstir-frontend build --workspace <path>` against a seeded workspace.
- `./utilities/format-build.sh` to guarantee both .NET and TypeScript formatting/build succeed.

## Rollout Notes
- Keep the existing C# handlers accessible behind a feature flag until the TypeScript pipeline proves stable.
- Update public docs and release notes once the frontend tarball ships with the CLI installer.
- Coordinate with CI to run the TypeScript package tests/linting before embedding artifacts.

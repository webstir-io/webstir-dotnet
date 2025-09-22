# Frontend Pipeline Cleanup Plan

## Goal
Gradually remove the C# asset pipeline stack under `Engine.Pipelines` once the TypeScript CLI fully owns build, publish, and watch workflows, without regressing developer tooling or breaking existing tests.

## Scope
- Target the `.NET` code paths that still depend on `Engine.Pipelines` (handlers, utilities, manifest helpers, DI bindings, tests).
- Leave the TypeScript CLI and packaging work tracked in `frontend-integration.md` untouched, referencing this plan only once those prerequisites land.
- Exclude backend/server pipelines; only remove or migrate the legacy frontend-specific code.

## Preconditions
1. `.tools/frontend-manifest.json` must feed all .NET consumers (watch mode, dev server, publishers, tests) so no runtime code reads `Files.ManifestJson` directly.
2. Shared utilities needed post-removal (e.g., CSP, SRI, precompression helpers) are relocated to neutral namespaces or replaced with TypeScript implementations.
3. `FrontendWorker` bridge work (package embedding, CLI invocation, diagnostics) is complete and feature-flagged as the default path.
4. Tests cover the manifest ingestion path and updated utilities, with snapshots/fixtures reflecting the TypeScript CLI outputs.

## Workstreams

### 1. Manifest Adoption & Consumers
- Introduce a typed manifest loader for `.tools/frontend-manifest.json` under `Engine.Frontend` (or similar) and replace direct `AssetManifest.Load` calls.
- Update `WebServer`, `WatchWorkflow`, and publish routines to take dependency-injected manifest services rather than touching `Engine.Pipelines.Core`.
- Adjust tests to read the new manifest representation; regenerate fixtures if hashing conventions differ.

### 2. Utility Relocation
- Move `ContentSecurityPolicy`, `SubresourceIntegrity`, precompression helpers, and related constants into a shared `Engine.Frontend` namespace.
- Provide TypeScript-side equivalents when runtime behavior has migrated; ensure the .NET side uses the relocated versions.
- Delete any duplication that the TypeScript CLI already performs (e.g., HTML hardening) and keep only what the dev server still needs.

### 3. Dependency Graph Cleanup
- Remove `IFrontendHandler` registrations from `CLI/Program.cs` and delete handler implementations once all references compile against the new services.
- Drop unused interfaces (`IFrontendHandler`, `IPageHandler`) and strip the pipeline folders from the project file.
- Update solution/project metadata (if any) and ensure `Engine.csproj` no longer includes removed directories.

### 4. Verification & Rollout
- Run `dotnet run --project Tests -- test build` and the quick suites to validate workflows after each major removal.
- Execute `./utilities/format-build.sh` to confirm formatting/build parity.
- Flip the feature flag (if present) that keeps legacy handlers reachable, monitor telemetry for CLI errors, then delete the flag and fallback code paths.
- Communicate the removal in release notes and internal docs, noting the TypeScript CLI as the sole frontend pipeline.

## Risks & Mitigations
- **Missed dependency on `Engine.Pipelines`** – use `dotnet build` with warnings-as-errors and `rg "Engine.Pipelines"` sweeps before deleting directories.
- **Incomplete manifest coverage** – ship the manifest loader with integration tests that fail if an expected entry is missing; block removal until green.
- **Dev server regressions** – stage manual smoke tests (`watch`, browser reloads) before cutting the feature flag.

## Validation Checklist
- [ ] All C# references to `Engine.Pipelines` are removed or replaced with new services.
- [ ] Tests under `Tests/Pipelines/**` are either migrated to new fixtures or removed with clear justification.
- [ ] `dotnet run --project Tests` (quick suite) passes on clean checkout.
- [ ] `./utilities/format-build.sh` completes successfully.
- [ ] Release notes and `docs/reference/frontend-manifest.md` updated to describe the new source of truth.

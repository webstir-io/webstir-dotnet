# Framework Console Plan

## Motivation
- Keep the user-facing `webstir` CLI focused on application workflows (init, build, watch, publish, test, install).
- Provide maintainers with a dedicated, discoverable entry point for framework package maintenance (rebuilding, verifying, publishing the embedded frontend/testing tarballs).
- Eliminate bash-only flows so CI, release automation, and contributors use the same command surface.
- Reduce confusion from maintainer-only commands showing up in end-user help output.

## Goals
- Introduce a `Framework` console application that owns package build/publish/verify commands.
- Share existing packaging infrastructure (`PackageBuilder`, synchronizer, manifest writer) without duplicating logic.
- Consolidate the existing framework workspaces (frontend, testing, manifest output) inside the `Framework` project directory.
- Relocate packaging bridge code (currently `Engine/Bridge/Packaging`) into the `Framework` project so the concern lives with the console.
- Keep dependency boundaries clean: the `Framework` project builds independently of `Engine`, and shared runtime pieces flow through explicit contracts.
- Update scripts, CI, and release workflows to call the new console app.
- Document maintainer workflows so contributors know when/how to use the console.
- Replace ad-hoc shell helpers with console-managed staging so tarballs and manifests are committed automatically.

## Non-Goals
- Changing how application builds work (`webstir build` remains unchanged).
- Replacing existing packaging services; the console should reuse them.
- Automatically running framework builds during every `dotnet build`; maintainers still trigger them explicitly.

## Architecture
1. **Framework Console Project**
   - New project `Framework` (binary name `framework`).
   - Builds without referencing `Engine`; instead it exposes the packaging services that `Engine` consumes where necessary.
   - Configures Serilog logging (matches main CLI style) and DI container with required services only.
   - Relocate current `framework/frontend`, `framework/testing`, and `framework/out` under the project so packaging assets and code ship together.

2. **Command Surface**
   - `framework build [--frontend|--test|--both] [--publish] [--verify]`
   - `framework publish` (shortcut for `build --publish --verify`).
   - `framework verify` (hash/manifest check only).
   - Simple argument parser; reuse existing change detection/staging logic where helpful.

3. **Shared Services**
   - Relocate the packaging services to the project (e.g., move `Engine/Bridge/Packaging` into a Framework-owned namespace) and expose helper methods/public records such as `CreateManifestMetadata` and `PackageBuildResult` for `Engine` to consume.
   - Keep logging messages scoped with `[framework]` to make CI logs easy to scan.

4. **Automation**
   - `framework build …` handles change detection and git staging internally; existing shell wrappers (e.g., `utilities/build-packages.sh`) are retired.
   - `.github/workflows/ci.yml` uses `dotnet run --project Framework -- verify`.
   - `.github/workflows/release.yml` uses `dotnet run --project Framework -- build --publish --verify`.
   - `utilities/format-build.sh`, `utilities/local-ci.sh`, and other helpers call the new console.

## Migration Steps
1. [x] Scaffold `Framework` project and add to the solution.
2. [x] Move existing `framework/frontend`, `framework/testing`, and `framework/out` into the project (adopt consistent subfolder naming) and update references.
3. [x] Bring over `Engine/Bridge/Packaging` and related helpers into the project (adjust namespaces/usings).
   - `PackageBuilder`, `PackageSynchronizer`, and the package installers now live under `framework/Packaging`, with Engine adapting via lightweight workspace wrappers.
4. [x] Extract or duplicate any shared utilities so `Framework` remains independent from `Engine`, then wire `Engine` to consume the new `Package*` contracts without creating circular references.
   - Packaging uses `IPackageWorkspace` + resource helpers under `framework/Resources/tools`, while Engine adapts via `PackageWorkspaceAdapter` and no longer embeds tool tarballs directly.
5. [ ] Implement command routing + logging + DI registration.
6. [ ] Update Engine workflows and helpers (`FrontendWorker`, `InstallWorkflow`, `TestPackageUtilities`, etc.) to call the new `Package*` contracts and namespaces.
7. [ ] Publicize necessary members from packaging services (`PackageBuilder`, etc.).
8. [ ] Port logic from the removed `packages` workflow into the new console commands.
9. [ ] Update the solution, build scripts, and CI to add the new project and remove the old `framework` folder wiring.
10. [ ] Update bash helpers, CI, release workflow, and docs to call the new console and new paths (remove `utilities/build-packages.sh`).
11. [ ] Remove the maintainer-only packages command from the main CLI (`Commands.Packages`, workflow, help entries`).
12. [ ] Document usage in `docs/how-to/framework-packages.md` and cross-link from README / plans, highlighting auto-staging behavior.

## Dependencies / Considerations
- Ensure Node/npm tooling is still required only when the console runs (not during normal builds).
- Update release automation alongside the console changes; compatibility with the old setup is not required.
- Provide clear errors when framework source directories are missing or npm commands fail.
- Auto-staging should respect existing contributor workflows (e.g., skip staging on dry runs, surface staged paths in logs).
- Packaging integration tests (`package` suites) need to move to the `Framework` console entry point so CI exercises the new commands.

## Testing Strategy
- Extend existing packaging tests or add new integration tests that exercise `framework build --verify` in CI.
- Validate release workflow in a dry run (ensure tags/releases still produce tarballs and manifest).
- Manual smoke test: modify frontend package, run `framework build`, confirm manifest/tarballs update, run `webstir install` in sample workspace.

## Current Packaging Map
- `framework/Packaging/PackageBuilder.cs`
  - Builds frontend/testing tarballs, updates manifests, optionally publishes to npm.
  - Consumed by `Engine/Workflows/PackageWorkflow` and registered via DI in `CLI/Program.cs`.
  - Produces artifacts under `framework/out`, `framework/Resources/tools`, and rewrites `Engine/Resources/package.json`.
- `framework/Packaging/PackageSynchronizer.cs`
  - Entry point for keeping workspace packages aligned.
- `framework/Packaging/FrontendPackageInstaller.cs`
  - Ensures the embedded frontend tarball, manifest, and package.json dependency stay in sync for a workspace.
- `framework/Packaging/TestPackageInstaller.cs`
  - Mirrors the frontend installer for the test harness package.
- All three rely on `PackageSourceSelector` and the Engine workspace adapter for npm operations, while embedded artifacts ship from `framework/Resources/tools`.
- `framework/Packaging/PackageSourceSelector.cs`
  - Shared utility respecting environment overrides for registry vs tarball sources.
- `framework/Packaging/FrameworkPackageRepository.cs`
  - Resolves `framework/out/manifest.json` (or `WEBSTIR_PACKAGE_ROOT`) for installers.
  - Used by both `FrontendPackageInstaller` and `TestPackageInstaller`.
- `framework/Packaging/IPackageEnsureResult.cs`
  - Implemented by `FrontendPackageEnsureResult` and `PackageEnsureResult`, enabling shared logging in workflows.
- Scripts & Docs referencing the legacy CLI
  - `utilities/build-packages.sh` calls `dotnet run --project FrameworkCLI -- build …`.
  - `utilities/local-ci.sh`, `utilities/format-build.sh`, and GitHub workflows continue to call `webstir packages …` (needs swap once the new console owns the surface).
  - `docs/how-to/framework-packages.md` documents the existing `FrameworkCLI` entry point and bash helper behaviour.
- Assets to relocate alongside the project
  - `framework/frontend`, `framework/testing`, and `framework/out` now sit alongside `Framework.csproj`; outputs remain tracked in `framework/out/manifest.json`.
  - Embedded tool copies live under `framework/Resources/tools` with manifests `frontend-package.json` and `testing-package.json`.

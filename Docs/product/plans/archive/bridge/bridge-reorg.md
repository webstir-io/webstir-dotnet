# Bridge Reorganization Plan

## Goals
- Make the ".NET bridge" an explicit, discoverable surface by introducing the `Engine.Bridge` namespace hierarchy.
- Consolidate CLI-orchestration utilities (frontend worker, package installers, runtime detection, manifest readers) under a single feature slice.
- Reduce duplication between TypeScript and .NET by leaning on manifest outputs instead of ad-hoc path construction.

## Current State Snapshot
- Frontend/Backend/Shared worker implementations now live under `Engine.Bridge.*`, while interfaces remain in `Engine.Interfaces` for workflow consumption.
- Manifest models (`FrontendManifest*`), SRI, and CSP helpers have been relocated to `Engine.Bridge.Frontend` so consumers share the CLI view of filesystem state.
- Docs reference a bridge concept, but some onboarding material still calls out the old `Engine/Workers` paths.
- Legacy `Precompression` helper has been removed now that the TypeScript CLI emits Brotli/GZip variants directly; lingering doc references still need cleanup.

## Scope & Non-Goals
- **In-scope:** Namespace/file reorg, mechanical reference updates, doc alignment, pruning fully-dead helpers encountered along the way.
- **Out-of-scope:** Behavior changes to CLI workflows, DevService/NodeServer feature work, or altering the TypeScript build outputs.

## Proposed Structure
```
Engine/
  Bridge/
    NodeRuntime.cs
    NpmHelper.cs
    Frontend/
      FrontendWorker.cs
      FrontendPackageInstaller.cs
      FrontendManifestLoader.cs
      FrontendManifest.cs
      SubresourceIntegrity.cs
      ContentSecurityPolicy.cs
    Test/
      TestCliRunner.cs
      TestCliModels.cs
      TestPackageInstaller.cs
      TestPackageUtilities.cs
    Backend/
      BackendWorker.cs
    Shared/
      SharedWorker.cs
```
- Follow-up slices (e.g., `Bridge/Backend`) can adopt the same convention once their pipelines are modernized.

## Workstreams
1. **Namespace Introduction**
   - Create `Engine/Bridge/Frontend` folder and update namespaces to `Engine.Bridge.Frontend` (or nested sub-names where helpful).
   - Adjust DI registrations, `using` directives, and tests that import the migrated types.

2. **Helper Migration**
   - Move manifest models/loader, SRI, and CSP helpers into the new folder.
   - Update downstream consumers (`WebServer`, `FrontendWorker`, tests) to point at the relocated files.

3. **Installer & Runtime Consolidation**
   - Relocate `FrontendPackageInstaller` under `Engine.Bridge.Frontend`, move `TestPackageInstaller` alongside the other testing helpers, and surface `NodeRuntime`/`NpmHelper` at the bridge root for shared use.
   - Verify embedded resource paths remain valid after the move (resource namespaces follow file location).

4. **Testing Bridge Alignment**
   - Establish `Engine.Bridge.Test` for the CLI testing bridge (`TestCliRunner`, models, and installer utilities).
   - Update workflows and docs that reference `Engine.Testing` to the new namespace.
   - Confirm JSON event parsing and package bootstrap continue to work after the relocation.

5. **Worker Relocation**
   - Move `FrontendWorker`, `BackendWorker`, and `SharedWorker` implementations under the bridge hierarchy (e.g., `Engine.Bridge.Frontend.FrontendWorker`).
   - Keep `IWorkflowWorker`/`IFrontendWorker` contracts in `Engine.Interfaces`; update DI registrations and tests to reference the relocated classes.
   - Document future runtime-specific bridge slices (TypeScript vs. potential Python) so workflows stay insulated from implementation details.

6. **Cleanup & Deprecations**
   - Follow through on documentation updates now that `Precompression.cs` has already been deleted; scrub lingering references or note the CLI ownership explicitly.
   - Update docs (`frontend-*` plans, explanations) to reference the new path / namespace.
   - Run `./utilities/format-build.sh` to ensure formatting and build checks succeed post-move.

## Validation
- `./utilities/format-build.sh`
- Targeted test suite: `dotnet run --project Tests -- test build`
- Manual smoke: `webstir build` to confirm the CLI still shells out correctly after namespace changes.

## Risks & Mitigations
- **Resource name mismatches:** Moving embedded resource files changes manifest resource IDs. Use CI + manual audit of generated tarballs to confirm installers still locate assets.
- **Namespace churn:** Large search/replace may miss call sites. Mitigate with compiler errors and `rg` sweeps for old namespaces.
- **Documentation drift:** Existing plans/tutorials mention old paths. Capture doc updates as part of the workstream to keep onboarding clean.

## Exit Criteria
- All frontend bridge C# files, including `FrontendWorker`, reside under `Engine/Bridge/Frontend` (or documented exceptions).
- Testing CLI orchestration types live under `Engine/Bridge/Test` with updated references.
- Backend and shared workers are queued/migrated into bridge subfolders once their pipelines adopt the bridge pattern.
- Consumers compile against the new namespaces without warnings.
- Docs and diagrams reference the new structure.
- Dead helpers are removed or clearly flagged with follow-up tasks.

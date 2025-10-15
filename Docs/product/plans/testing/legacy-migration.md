# Testing Legacy Migration Plan

## Objective
Complete the transition from the bespoke `Tests` harness to the `Tester` xUnit project by re-implementing every workflow, pipeline, and framework-package test directly in the new structure and deleting the legacy code.

## Scope
- Move all remaining `Tests/**/*.cs` logic (workflows, pipelines, installers, framework packages, helpers) into `Tester/`.
- Recreate shared infrastructure (`WorkspaceManager`, process helpers, assertions) inside `Tester.Infrastructure`.
- Remove compatibility shims once no code references the legacy interfaces/types.

## Phases

### Phase 1 – Shared Infrastructure Extraction
- Add first-class helpers in `Tester.Infrastructure` for:
  - `ProcessRunner`, `ProcessRunOptions`, `TerminationMethod`
  - CLI runner wrapper
  - `WorkspaceManager`, seed baseline utilities, path helpers
  - Assertion extensions needed across tests
- Update existing xUnit tests to use the new helpers instead of `Tests.Framework.*`.
- Delete the matching files from `Tests/Framework` after replacement.

### Phase 2 – Workflow Suites Migration
Port workflow cases one suite at a time, inlining their behavior into `[Fact]` methods:
1. `Init` (default/named project)
2. `Build` (happy path, missing app.html)
3. `Publish` (success + TypeScript diagnostics)
4. `Watch`
5. `Help`
6. `Add` (page/test generators)
7. `Test` (backend test execution)
- For each suite:
  - Copy any helper logic into `Tester/Workflows/<Suite>/`.
  - Remove the legacy files under `Tests/Workflows/<Suite>/` once migrated.

### Phase 3 – Pipeline & Installer Tests
- Convert CSS/HTML/JS/Core pipeline tests to native xUnit facts, referencing shared helpers and snapshot assets.
- Move snapshots to `Tester/Pipelines/.../__snapshots__`.
- Migrate package installer tests (`TarballDependencyUpdate`) into `Tester`.
- Delete corresponding directories under `Tests/Pipelines` and `Tests/PackageInstallers`.

### Phase 4 – Framework Package Suites
- Port `PackageAutomationUnitTests`, `PackageAutomationIntegrationTests`, and `ReleaseNotesSnapshotTests` into `Tester/FrameworkPackages`.
- Recreate temporary repository/test workspace helpers as needed.
- Remove the legacy files in `Tests/FrameworkPackages`.

### Phase 5 – Final Cleanup
- Remove any remaining references to `Tests.*` in `Tester.csproj`.
- Delete the `Tests` directory entirely.
- Drop compatibility shims (`LegacyTestCaseExecutor`, `LegacyTestSuiteExecutor`) once unused.
- Run `dotnet test Tester/Tester.csproj` (quick + full) to confirm parity.
- Update documentation to reflect the completed migration.

## Risks & Mitigations
- **Regression risk**: migrate suite-by-suite with tests green after each step.
- **Shared helper divergence**: centralize utilities early (Phase 1) to avoid duplication.
- **Snapshot churn**: move assets carefully and keep path references consistent.

## Success Criteria
- No `Tests/` directory or legacy abstractions remain.
- The entire CLI/Engine coverage runs under `dotnet test Tester/Tester.csproj`.
- Documentation and scripts reference only the new test project.

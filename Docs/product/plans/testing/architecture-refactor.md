# Tester Architecture Refactor Plan

## Overview
- Migrate the bespoke CLI-based test harness to a first-class `.NET` test project named `Tester` that executes under `dotnet test`.
- Reduce end-to-end runtime by embracing framework-managed parallelism and shared fixtures.
- Preserve existing workflow coverage (CLI init/build/publish/watch/test) while improving observability and tooling integration.

## Goals
- Adopt xUnit as the canonical framework for all automated tests.
- Enable execution via `dotnet test` locally and in CI without compatibility shims.
- Maintain the current quick/full suite split through standard trait filtering.
- Leverage fixtures to minimize redundant workspace initialization and dependency installs.
- Produce test outputs compatible with IDE tooling (VS, Rider) and common CI reporters.

## Non-Goals
- Rewriting the CLI workflows themselves.
- Introducing new workflow scenarios beyond today’s coverage.
- Building backwards compatibility layers for the legacy runner.

## Background
The existing `Tests` project (to be replaced by `Tester`) is an executable that orchestrates custom `ITestSuite`/`ITestCase` implementations. It depends on shared helpers (`WorkspaceManager`, `ProcessRunner`, CLI wrappers) to bootstrap seed workspaces and run external commands. While it offers rich coverage, the custom harness:
- Cannot be invoked via `dotnet test`, limiting IDE tooling and CI integration.
- Serializes many scenarios, inflating runtime.
- Requires bespoke reporting/diagnostics plumbing.

The migration will streamline execution by leaning on xUnit primitives while reusing the domain-specific helpers that interact with the CLI.

## Design Tenets
- **Reuse over rewrite**: Keep CLI/process helper code; wrap existing scenarios in xUnit facts.
- **Parallel where safe**: Organize tests into xUnit collections to isolate stateful workflows and allow read-only checks to run concurrently.
- **Fixture-first setup**: Cache expensive setup (seed workspace, npm install) in collection fixtures; ensure deterministic cleanup.
- **Lean assertions**: Replace bespoke assertions with xUnit equivalents except where domain-specific helpers add clarity.

## Target Architecture
1. Replace the executable project with `Tester/Tester.csproj`, configured with `Microsoft.NET.Test.Sdk`, xUnit packages, and no `Main` entry point.
2. Introduce:
   - `CollectionFixtures` for seed workspace preparation and shared CLI context.
   - `Trait` constants mirroring `TestCategory` (`Quick`, `Full`).
   - Helper adapters (`WorkflowFact` base class) to bridge current `ITestCase` logic into `[Fact]` methods.
3. Delete legacy abstractions (`ITestSuite`, `ITestRunner`, `TestOutputManager`) once all suites are migrated.
4. Update scripts (`utilities/format-build.sh`, CI definitions) to call `dotnet test`.

## Phased Migration
### Phase 1: Foundations
- Add xUnit dependencies and test SDK configuration within the `Tester` project.
- Implement shared fixtures for seed workspace / CLI helpers.
- Port the Init workflow tests as a spike to validate the structure.

### Phase 2: Workflow Migration
- Port remaining workflow suites (Build, Publish, Watch, Help, Add, Framework packages, Package installers) into `Tester`.
- Introduce `[Trait]` attributes and ensure quick/full toggles respect current behavior.
- Remove corresponding legacy suite classes after each migration.

### Phase 3: Cleanup & Tooling
- Remove unused infrastructure (`Tests/Program.cs`, `TestSuite` base, custom output manager`) after the `Tester` migration.
- Update developer docs (`README`, `.codex/testing.md`, internal runbooks) to reference `Tester` and `dotnet test`.
- Adjust CI pipelines and `format-build.sh` to run the new test command.

### Phase 4: Performance Optimization
- Enable xUnit parallelization for non-mutating collections.
- Tune workspace copying/fixtures (e.g., node_modules symlink strategy) to minimize setup time.
- Add diagnostics/tracing to surface slow tests and refine collection boundaries.

## Risks & Mitigations
- **Workspace contention**: Mutating workflows could interfere when parallelized. Mitigate with collection scoping and defensive copy/symlink strategies.
- **Timeout tuning**: CLI commands run longer under load; establish clear defaults and per-test overrides.
- **CI environment parity**: Ensure fixtures handle Linux/macOS differences (symlink support) gracefully.

## Success Criteria
- `dotnet test` succeeds locally without custom flags by targeting the `Tester` project.
- CI pipelines adopt the new command and complete within agreed SLAs.
- Developers can filter quick/full suites via traits.
- Legacy harness code is fully removed with no loss of scenario coverage.

## Open Questions
- Should we publish test result artifacts (TRX, JSON) for downstream analytics?
- Do we need integration with coverage tooling (Coverlet) once under `dotnet test`?
- Any upcoming workflows that require additional fixtures or dedicated collections?

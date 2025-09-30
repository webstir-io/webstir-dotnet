# Workspace Test Runner Unification Plan

## Goal
Fold the existing C#-based test discovery/runner (`Engine.Pipelines.Test`) into the TypeScript workspace so the `framework/testing` package exposes the canonical `webstir test` CLI for both frontend and backend scenarios.

## Current State
- The frontend build/publish flows already shell into `@electric-coding-llc/webstir-frontend` and rely on the manifest for asset metadata.
- Test workflows (`watch`, `test`) now ensure the bundled `@electric-coding-llc/webstir-test` package and shell into the unified `webstir-test` CLI for both frontend and backend suites.
- The TypeScript CLI owns discovery, manifest generation, and node-based execution for both runtimes while preserving the JSON event stream contract for future host specialization.

## Workstreams

### 1. CLI Design & Packaging
- Promote `framework/testing` to a runnable CLI (`bin/webstir-test`), keeping the package scope under the existing tarball embedding flow.
- Define command surface (`test`, `watch`, per-suite flags) mirroring today’s .NET commands for backwards compatibility.

### 2. Test Discovery & Mapping
- Port the logic from `Engine.Pipelines.Test.TestDiscovery` and related helpers into TypeScript modules that understand both frontend (`src/frontend/**/tests`) and backend (`src/backend/**/tests`) layouts.
- Generate a normalized manifest (JSON) describing discovered tests, build output locations, and required runtimes.

### 3. Execution Engine
- Implement runners for:
  - Frontend: execute compiled JS bundles via Node with structured diagnostics (reuse the existing harness assertions).
  - Backend: execute compiled backend JS with the same Node harness while keeping the hook points ready for a future dedicated host.
- Emit a unified JSON event stream (start, pass, fail, diagnostics) for the .NET bridge to relay without parsing text.

### 4. .NET Bridge Simplification
- Update `TestWorkflow` and `WatchWorkflow` to shell into the new CLI (`webstir-test`) after ensuring the tarball.
- Remove `Engine.Pipelines.Test` namespace, interfaces, and DI registrations once the CLI is stable.

### 5. Tooling & Automation
- Update `utilities/format-build.sh`, CI jobs, and local docs to invoke the TypeScript CLI for tests.
- Ensure tarball embedding (`framework/Resources/tools`) includes the new package version and regenerate hashes.

## Risks & Mitigations
- **Backend runner drift** – Mitigate by keeping the existing .NET host as the execution engine in early phases (TypeScript CLI spawns it) before considering a full port.
- **Telemetry regressions** – Preserve structured logging by defining explicit JSON events; add coverage in integration tests that assert key fields.
- **Developer adoption** – Provide transitional aliases (`webstir test` in .NET still works) that simply call the new CLI so scripts don’t break.

## Validation Checklist
- [x] `webstir-test` CLI discovers both frontend and backend tests from a clean workspace and executes them through the unified node runner.
- [x] `webstir-test watch` reuses change detection and emits pass/fail summaries matching current behavior.
- [x] `TestWorkflow` and `WatchWorkflow` compile without referencing `Engine.Pipelines.Test` and shell only into the new CLI.
- [x] Embedded tarball hashes updated (`frontend-package.json`, `testing-package.json`) and `dotnet run --project Tests` passes using the new runner.
- [x] Documentation refreshed: developer guide, zero-config expectations, and release notes.

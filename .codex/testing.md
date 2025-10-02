# Testing Guide

## Philosophy
- Tests protect the developer experience by covering real workflows instead of internal implementation details.
- Validate end-to-end scenarios (init → build → run) rather than isolated units.
- Lock down public contracts: CLI behavior, generated files, directory structure, and build results.
- Use snapshot tests for scaffolding outputs and property-style checks for broad invariants.
- Accept that observability complements testing; make runtime issues visible and actionable.

## What We Skip
- Test-driven development for every change.
- Exhaustive unit coverage of private helpers.
- Chasing coverage percentages as a success metric.

## Scope Guidelines
- Must test: core workflows, public contracts, scaffolding outputs, and critical invariants.
- May test: performance characteristics, optional integrations, rare edge cases.
- Won’t test: transient implementation details or private helpers that do not affect user experience.

## Running Tests
- Quick run of default suites (`init`, `build`, `publish`):
  - `dotnet run --project Tests`
- Full suite (adds `watch`, `help`, `add`):
  - `dotnet run --project Tests -- --full`
  - Or set `WEBSTIR_TEST_MODE=full`
- Run a specific suite:
  - `dotnet run --project Tests -- test <suite>` where `<suite>` is `init`, `build`, `publish`, `watch`, `help`, or `add`
- Runner help:
  - `dotnet run --project Tests -- help`

## Requirements
- .NET 9 SDK installed.
- Node.js and `tsc` available on `PATH` for tests that emit TypeScript builds.
- The repo uses a custom runner; `dotnet test` will not execute these suites.
- Run `./utilities/format-build.sh` before or after testing to fix formatting drift, refresh toolchain packages, and catch build failures early.

## Seed Workspaces & Baselines
- Prefer `WorkspaceManager.CreateSeedWorkspace(context, <scenario>)` inside tests instead of invoking CLI scaffolding commands. Scenario names (e.g., `seed-build`, `seed-tree`, `html-perf`) ensure each test gets an isolated copy while reusing the shared baseline content.
- Scenario-specific tweaks (injecting perf CSS/HTML, feature-flag configs, etc.) live in helper utilities such as `HtmlPublishScenarios`. Keep those manipulations deterministic so repeated publishes remain stable.
- We intentionally do **not** check in additional fixture directories under `Tests/.baselines`; the runtime mutations keep seed copies aligned with the embedded tarballs. If you add a new scenario, document the tweak in the helper and keep the mutation minimal.

## In One Line
> Test the experience, not the implementation.

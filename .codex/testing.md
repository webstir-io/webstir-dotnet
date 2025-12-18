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
- Quick smoke (defaults to `Category=Quick`):  
  - `dotnet test Tester/Tester.csproj`
- Full suite (includes watch/help/add/framework packages):  
  - `WEBSTIR_TEST_MODE=full dotnet test Tester/Tester.csproj`  
  - or `dotnet test Tester/Tester.csproj --filter "Category=Full"`
- Targeted category or namespace:  
  - `dotnet test Tester/Tester.csproj --filter "Category=Quick&FullyQualifiedName~Tester.Workflows.Build"`
- Collect coverage (optional baseline):  
  - `dotnet test Tester/Tester.csproj /p:CollectCoverage=true`

## Requirements
- .NET 10 SDK installed.
- Node.js and `tsc` available on `PATH` for tests that emit TypeScript builds.
- Tester executes under the standard `dotnet test` harness; no custom runner required.
- Run `./utilities/format-build.sh` before or after testing to fix formatting drift, refresh toolchain packages, and catch build failures early.

## Seed Workspaces & Baselines
- Prefer `WorkspaceManager.CreateSeedWorkspace(context, <scenario>)` inside tests instead of invoking CLI scaffolding commands. Scenario names (e.g., `seed-build`, `seed-tree`, `html-perf`) ensure each test gets an isolated copy while reusing the shared baseline content.
- Scenario-specific tweaks (injecting perf CSS/HTML, feature-flag configs, etc.) live in helper utilities such as `HtmlPublishScenarios`. Keep those manipulations deterministic so repeated publishes remain stable.
- We intentionally do **not** check in additional fixture directories under `Tests/.baselines`; the runtime mutations keep seed copies aligned with the embedded resources. If you add a new scenario, document the tweak in the helper and keep the mutation minimal.

## In One Line
> Test the experience, not the implementation.

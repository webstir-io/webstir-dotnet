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
- Run `./utilities/format-build.sh` before or after testing to fix formatting drift and catch build failures early.

## In One Line
> Test the experience, not the implementation.

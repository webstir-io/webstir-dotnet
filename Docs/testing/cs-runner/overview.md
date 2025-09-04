# Workflow-Based Test Organization

## Why
- Improve maintainability: avoid giant, growing test files per command.
- Preserve behavior: keep CLI contracts and outputs unchanged.
- Enable scale: add tests by adding files, not by inflating methods.
- Keep quick vs full runs clean and explicit.

## What (At a Glance)
- One folder per workflow: Init, Build, Publish, Watch, Help.
- Each workflow has a single suite file named `<Workflow>Tests.cs` (implements `ITestSuite`).
- Each test case is a separate class/file (implements `ITestCase`).
- Helpers can live per-workflow or in `Tests/Framework` as needed (no dedicated shared folder initially).

## Status
- Migrated suites: Init, Build, Publish, Watch, Help
- Legacy `Tests/Suite` directory removed after migration

## Non-Goals
- No changes to the CLI flags, commands, or exit codes.
- No changes to the end-to-end behavior of builds and publishes.
- No external test frameworks.

## Principles
- Test the experience, not the implementation.
- Favor contracts and invariants over micro unit coverage.
- Prefer small, readable test files with descriptive names.
- Keep a clear “Quick” vs “Full” categorization to speed local dev while preserving deeper checks.

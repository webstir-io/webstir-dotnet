# Migration Plan (Incremental, Behavior-Preserving)

This plan keeps all current tests green while you migrate to the workflow-based structure.

## Phase 1 — Framework Scaffolding
- Add `ITestCase`, `TestCategory`, `TestCaseContext`, and optionally `TestCaseBase`.
- Optionally add simple helpers (`Cli`, `SeedProjectFixture`) either under `Tests/Framework` or inline within workflow folders as they become necessary.
- Keep existing suites (`InitTests`, `BuildTests`, `PublishTests`, `WatchTests`, `HelpTests`) unchanged for now.

Outcome: No behavior changes. All tests still pass.

## Phase 2 — Publish Workflow Split
- Create `Tests/Workflows/Publish/PublishTests.cs` and place case files directly in the same folder (no nested `Tests/`).
- Split `TestPublishCommandSuccess` into focused cases:
  - `PublishRunsWithoutErrors`
  - `ClientArtifactsExist`
  - `JsIsMinified`
  - `CssIsMinified`
  - `HtmlWhitespaceCollapsed`
  - `ManifestIntegrity`
- Register `Tests.Workflows.Publish.PublishTests` in DI (Program.cs). Remove or shim the old monolithic `Tests/Suite/PublishTests.cs`.

Outcome: Behavior unchanged; cases are now focused and easier to extend. Legacy suites under `Tests/Suite` can be removed once the new workflow suites are registered (done).

## Phase 3 — Build/Init/Watch/Help Splits
- Repeat Phase 2 for other workflows.
- Convert suite-wide setup/cleanup to `SeedProjectFixture` usages per case as needed.
- Move any suite-specific helpers into `Workflows/<Workflow>/Helpers`.

Outcome: All suites adopt the same pattern. Legacy files under `Tests/Suite` removed.

## Phase 4 — Polishing (Optional)
- Introduce reflection-based discovery if desired (reduce manual maintenance).
- Add tags like `Smoke` or `Perf` if needed.
- Add a basic contract/snapshot check for generated scaffolding outputs.

## Mapping (Publish Example)

The current `PublishTests.cs` combines many checks. A possible mapping:
- Seed/setup → `SeedProjectFixture` in each case or a tiny shared helper
- Exit code and no-compilation-errors → `PublishRunsWithoutErrors`
- Dist exists, page artifacts exist → `ClientArtifactsExist`
- JS minification checks → `JsIsMinified`
- CSS minification checks → `CssIsMinified`
- HTML whitespace collapse → `HtmlWhitespaceCollapsed`
- Manifest-driven JS path and integrity → `ManifestIntegrity`

## Guardrails
- Keep `--quick` the default; mark expensive cases as `Full`.
- Maintain use of `AppWorkspace`/`Folders`/`Files` constants for paths.
- Avoid duplication; if helpers emerge, place them per-workflow or in `Framework` later.

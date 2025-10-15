# Package Release Automation – Phase 6 Testing & Rollout

## Goals
- Validate the unified CLI release flow across realistic scenarios before cutting over CI and production workflows.
- Establish automated coverage that protects the new helpers, change detection, version bump heuristics, and publish safeguards.
- Roll out the automation incrementally with clear checkpoints, observability, and rollback plans.

## Test Coverage Additions
- Add unit tests for the package metadata service, git diff service, process runner, and credential manager. Cover success and failure paths, including lockfile updates, missing tokens, and registry overrides.
- Introduce integration tests under `Tests` that execute the CLI end-to-end: `packages bump`, `packages sync`, `packages release`, and `packages publish --dry-run`. Seed workspaces should simulate single-package changes, multiple-package updates, and no-op runs.
- Capture JSON or structured summaries emitted by the CLI and assert their schema to prevent regressions that would break CI consumers.
- Expand snapshot coverage for release notes generation to verify grouping by commit type and the `--no-notes` escape hatch.

## End-to-End Scenarios
- **Single package change** – Modify one package manifest and confirm only that package bumps, rebuilds, and (optionally) publishes.
- **Multiple packages** – Update both frontend and testing packages. Ensure the CLI parallelizes or sequences work predictably and publishes both artifacts.
- **No-op release** – Run the pipeline with no changes; verify early exit with a zero status code and helpful messaging.
- **Credential failures** – Simulate missing `GH_PACKAGES_TOKEN` and misconfigured `.npmrc`. Assert the CLI surfaces actionable errors and skips publication.
- **Backend stub detection** – Ensure disabled packages remain untouched during bump/sync/publish while still appearing in summaries.

## Rollout Plan
- Run the full suite (`WEBSTIR_TEST_MODE=full dotnet test Tester/Tester.csproj`) on every PR touching the release tooling until stability is proven. Require green status before merge.
- Pilot the CLI release flow on an internal branch: generate tarballs, review release notes, and publish to a staging registry. Gather feedback from maintainers and record issues.
- Update GitHub Actions to run the CLI in parallel with the legacy scripts for one release. Compare outputs, tarball hashes, and publish logs to confirm parity.
- After a successful pilot, switch CI to the CLI-only flow and archive script shims. Communicate the cutover via release notes and internal channel updates.
- Schedule a post-cutover review to audit telemetry, retry rates, and feedback. Plan follow-up fixes before declaring the automation GA.

## Observability & Telemetry
- Emit structured logs (JSON) for each package step (bump, build, publish) with timestamps and durations. Ensure CI captures these as artifacts.
- Integrate failure metrics with the existing monitoring pipeline (e.g., GitHub Actions summaries or dashboards) so regression spikes are visible.
- Surface a dry-run summary file under `artifacts/packages-release-summary.json` for every run to aid manual verification and to unblock downstream tooling.

## Known Risks & Mitigations
- **Flaky external tooling** – npm or registry hiccups can break the pipeline. Implement retry-with-backoff for publish steps and feature flag it for testing.
- **Insufficient fixture coverage** – Expand seed workspaces to represent new package types (backend stub, optional integrations) so tests catch regressions early.
- **CI runtime cost** – New tests may extend pipeline duration. Parallelize scenarios where possible and reuse cached node modules/tarballs between runs.
- **Rollback complexity** – Keep the legacy scripts on a maintenance branch for one release cycle so we can restore them quickly if required.

## Exit Criteria
- Automated tests cover all critical helpers and CLI pathways, and the suite runs reliably in CI.
- End-to-end scenarios (single, multiple, no-op, credential failure, stub detection) are green in CI and documented.
- The CLI release flow ships in CI without legacy script fallbacks, and the first production release completes successfully.
- Observability artifacts (logs, summaries, metrics) exist and enable quick diagnosis of future release issues.
- A retrospective captures lessons learned and collates any remaining follow-up work.

## Testing Performance Plan

### Goal
Bring `./utilities/run-tests.sh` runtime under 45 seconds locally without sacrificing CI coverage.

### Proposed Workstreams
1. **Reduce publish invocations in HTML pipeline tests**
   - Audit `Tests/Pipelines/Html` to group assertions by scenario and reuse a single publish output per scenario.
   - Refactor the suite to prepare shared workspaces (e.g., default, feature-flags disabled, perf page) and execute multiple assertions against each publish run.
   - Measure time savings after the refactor to confirm fewer CLI calls translate to shorter runtime.

2. **Preseed specialized baselines** *(Done)*
   - ✅ Inventory the tests that scaffold via CLI (JS tree-shaking, perf page, build/watch flows).
   - ✅ Refactor those tests to reuse `WorkspaceManager.CreateSeedWorkspace` with scenario-specific names instead of invoking CLI scaffolding.
   - ✅ Decision: keep runtime mutations for now (no additional baseline fixtures required).
   - ✅ Documented the rationale and maintenance notes in `.codex/testing.md`.

3. **Parallelize/Async pipeline suites** *(In Progress)*
   - ✅ Execute CSS, JS, HTML, and Core pipeline suites concurrently inside `PublishTests` so we have the orchestration in place.
   - ✅ Ensure each suite clones its own scenario-specific seed workspace (`WorkspaceManager.CreateSeedWorkspace`) so overlapping runs do not contend for the same directories.
   - ✅ Wrap suites in `Task.Run` before `Task.WhenAll` so the CLI publish/build work actually runs on background threads.
   - 🔄 Re-measure publish timings and watch for flaky behaviour; if the gain is minimal, consider deeper async refactors inside the suites themselves.

5. **Instrumentation & Monitoring**
   - Keep the slow-test reporting added to `ITestResultFormatter` and consider exporting metrics per run to track regressions.
   - Configure CI to surface the slow-test section so future slowdowns are caught early.

### Next Steps
- Size and prioritize the workstreams with the team.
- Decide whether to tackle workstreams sequentially or in parallel (they are largely independent).
- After implementing each workstream, rerun `./utilities/run-tests.sh` and log the new timing in this document.

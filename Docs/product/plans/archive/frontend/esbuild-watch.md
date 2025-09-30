# Esbuild Watch Mode Plan

## Goal
Adopt esbuild's incremental `context.watch()` flow inside the frontend CLI so development rebuilds reuse cached graphs while keeping Webstir's integrated dev server, tests, and backend restart loop intact.

## Desired Outcomes
- Sub-150 ms rebuilds for typical page/component edits without spawning new Node processes.
- A long-lived frontend worker process that streams build status back to the .NET CLI runtime.
- No regressions to HTML/CSS/static asset outputs, manifests, or SSE reload behavior.
- Straightforward rollback to the current per-invocation CLI execution model.

## Current State
- `FrontendWorker` launches `webstir-frontend build`/`rebuild` on every change, then exits (`Engine/Bridge/Frontend/FrontendWorker.cs:66`).
- `DevService` coordinates the ASP.NET web server, Node backend server, and change notifications (`Engine/Services/DevService.cs:23`).
- Live reload uses SSE via `WebServer.UpdateClientsAsync` and a lightweight browser badge (`Engine/Servers/WebServer.cs:163`, `Engine/Resources/src/frontend/app/refresh.js:1`).
- The TypeScript CLI runs the complete pipeline (JS, CSS, HTML, assets) during each invocation (`framework/frontend/src/pipeline.ts:9`).

## Drivers
- Repeated cold esbuild builds dominate watch iteration time; caching the module graph should yield immediate wins.
- Vite-level HMR is out of scope for now, but incremental rebuilds narrow the DX gap without replacing the dev server.
- Maintaining a single orchestrator (dotnet CLI) avoids the coordination cost of running multiple dev servers.

## Proposed Architecture
1. **Persistent CLI Daemon**: Introduce a new command (e.g. `webstir-frontend watch-daemon`) that starts once, creates esbuild contexts per entry, and calls `context.watch()` to handle rebuilds in-place.
2. **IPC Channel**: Use stdout JSON or a dedicated pipe/socket so the daemon can publish build lifecycle events (start, success, failure, diagnostics, affected pages).
3. **Bridge Updates**: Replace repeated CLI invocations with a long-lived process managed similarly to `NodeServer`. The bridge relays change notifications to the daemon and receives completion events.
4. **Pipeline Coordination**: Non-JS builders (CSS, HTML, static assets) run inside the daemon after esbuild signals which pages changed. For now they can remain sequential; future iteration can add smarter invalidation.
5. **Reload Flow**: When .NET receives a success event it continues using `UpdateClientsAsync` to push SSE `reload` messages. Failure events surface in the console without triggering reloads.
6. **Shutdown Semantics**: `DevService.StopAsync` terminates the daemon gracefully, ensuring esbuild contexts dispose cleanly.

## Workstreams

### 1. CLI Watch Daemon
- Design new command entry in `framework/frontend/src/cli.ts` with lifecycle hooks (start, stop, reload commands).
- Build `WatchCoordinator` in the CLI to own esbuild contexts, route file change intents, and persist outputs to disk.
- Emit structured diagnostics compatible with existing `FrontendCliDiagnostic` parsing.

### 2. Incremental Build Plumbing
- Refactor JS builder to lazily create/lookup contexts and update page manifests only when outputs change.
- Gate CSS/HTML/static rebuilds behind change detection (initially coarse: rerun when page touched; refine later).
- Ensure the daemon writes `build/frontend` artifacts atomically so the ASP.NET server never serves partial files.

### 3. .NET Bridge Integration
- Extend `FrontendWorker` to start the daemon once, forward change events through stdin or IPC, and await responses.
- Mirror the process supervision pattern used by `NodeServer` for restart and shutdown, including log forwarding.
- Update `DevService` to treat frontend watch completion as asynchronous callbacks rather than awaiting a Task per invocation.

### 4. Dev Server & Reload UX
- Preserve SSE reload semantics but avoid duplicate reloads when multiple events coalesce; consider batching or debouncing notifications.
- Decide whether to retain the existing refresh badge or leverage daemon event types to show richer status (success/fail) in the badge.

### 5. Observability & Tooling
- Add verbose logging modes for the daemon to aid debugging (module graph stats, cache hits).
- Document operational commands (start/stop, manual rebuild trigger) and failure recovery steps.
- Update developer docs to describe the new watch architecture and fallbacks.

## Milestones
1. **Prototype**: Daemon builds a single page project incrementally, manual invocation only.
2. **Bridge Integration**: `.NET watch` workflow launches daemon, rebuilds JS incrementally, reloads browser.
3. **Full Pipeline Support**: CSS/HTML/static assets remain correct under incremental builds; manifests stay in sync.
4. **Stabilization**: Logging, error handling, shutdown, and rollback path complete; documentation and validation checklist satisfied.

## Risks & Mitigations
- **IPC Deadlocks**: Use async I/O with bounded queues; include heartbeats so `.NET` can detect stalled daemons.
- **Stale Outputs**: Hash or timestamp outputs after rebuild to verify changed files actually update; add integration tests covering repeated edits.
- **Process Lifecycle Drift**: Mirror existing `NodeServer` management to avoid orphan processes; provide explicit kill command on shutdown.
- **Mixed Platform Issues**: Validate watch mode on Windows/macOS/Linux with path normalization in IPC messages.
- **Fallback Complexity**: Keep the current build/rebuild commands and guard daemon usage behind a feature flag until stable.

## Validation Checklist
- `webstir watch` runs end-to-end with incremental rebuilds on macOS, Linux, Windows.
- Rebuild loop handles JS edits, CSS edits, HTML edits, and asset changes without stale files.
- Browser reload indicator updates appropriately on success/failure.
- Node backend restart flow remains unaffected.
- All automated suites referenced in `.codex/testing.md` plus targeted integration tests pass.
- Manual smoke: edit -> rebuild -> reload cycle under 500 ms for sample project; daemon shuts down cleanly on Ctrl+C.

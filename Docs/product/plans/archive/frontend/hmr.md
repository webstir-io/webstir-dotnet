# Frontend HMR Plan

## Goal
Deliver fast, framework-agnostic hot module replacement (HMR) for Webstir's dev workflow by layering granular asset updates on top of the new esbuild watch daemon, while preserving the existing SSE reload safety net.

## Desired Outcomes
- JS and CSS edits apply in-place without full page reloads when a module opts into HMR.
- HTML or manifest-affecting changes continue to trigger the current reload flow automatically.
- The watch daemon, .NET bridge, and dev server expose explicit success/fallback telemetry so regressions surface quickly.
- HMR integrates seamlessly with the existing reload loop so developers still have a reliable fallback when needed.

## Current State
- `WatchCoordinator` drives incremental rebuilds via esbuild contexts per page (`framework/frontend/src/watch/watchCoordinator.ts`).
- `.NET` bridge forwards daemon diagnostics and triggers reloads on `frontend.watch.pipeline.success` (`Engine/Bridge/Frontend/FrontendWatcher.cs`).
- `ChangeService` always publishes `ClientNotificationType.Reload` after a successful change (`Engine/Services/ChangeService.cs`).
- The dev server exposes a single SSE channel used by `refresh.js` to reload the page or display build status (`Engine/Servers/WebServer.cs`, `Engine/Resources/src/frontend/app/refresh.js`).
- No module-level diff metadata flows from esbuild to the browser; every success results in a full reload.

## Architecture Overview
1. **Watch Diagnostics**: Extend `WatchCoordinator` to emit hot-update payloads (changed modules, styles, reload requirements) when builds succeed.
2. **Bridge Propagation**: Update `FrontendWatcher` to capture the payload, surface it to C#, and expose a new `ClientNotificationType.HotUpdate` event.
3. **Dev Server Streaming**: Add SSE support for `hmr` events, keeping the existing debounce reload as a fallback.
4. **Browser Runtime**: Ship a lightweight `hmr.js` companion to `refresh.js` that dynamically imports modules, swaps CSS links, and calls page-level accept/dispose hooks.
5. **Fallback Logic**: When a payload declares `requiresReload` or the runtime declines the change, fall back to `reload` and record a diagnostic for telemetry.

## Workstreams

### 1. Daemon Instrumentation
- Capture esbuild rebuild timing and output metadata by requesting `metafile: true` on contexts when verbose mode is enabled.
- Diff previous and current outputs to determine which JS and CSS assets changed.
- Emit `frontend.watch.pipeline.success` diagnostics with `data.hotUpdate = { modules, styles, changedFile, requiresReload }`.
- Detect non-reload-safe edits (HTML builders, manifest changes) and set `requiresReload = true` to short-circuit HMR.
- Emit `frontend.watch.pipeline.hmrfallback` if any builder refuses hot updates after initially reporting them.

### 2. Bridge + ChangeService Integration
- Extend `FrontendWatcher` to deserialize `hotUpdate` payloads and raise a new event/callback when present.
- Update `ChangeService` to distinguish between `BuildSucceeded` and `HotUpdate`. Only send `Reload` for the former.
- Introduce `ClientNotificationType.HotUpdate` in `Engine/Models/ChangeNotifications.cs` and propagate through `NotifyClientsAsync`.
- Ensure pending command completions still resolve so the debounce logic remains intact.

### 3. Web Server Enhancements
- Add an SSE helper that writes `event: hmr` envelopes with compact JSON payloads.
- Preserve `UpdateClientsAsync` debounce for true reloads and ensure the same path runs when `requiresReload` is set.
- Stream per-step status (`building`, `success`, `error`, `hmr-fallback`) to keep the browser badge accurate.

### 4. Browser Runtime
- Introduce `Engine/Resources/src/frontend/app/hmr.js` that:
  - Listens for `hmr` SSE events.
  - Applies cache-busting query parameters and `import()`s each module URL sequentially.
  - Calls `window.__webstirAccept?.(module, context)` and waits for `true`/`false` to decide whether to reload.
  - Swaps CSS by cloning `<link rel="stylesheet">` nodes and disposing the old tags after load.
  - Emits console diagnostics to aid debugging.
- Update page scaffolds (if any) to register accept handlers or expose helpers in shared app bootstrap code.
- Keep the runtime lightweight and always-on so edits apply immediately without additional configuration.

### 5. Observability & Telemetry
- Count hot updates vs reload fallbacks in diagnostics.
- Add verbose logs when the daemon declines HMR (e.g., HTML builder touched) so developers understand the fallback.
- Optionally, expose a CLI `--hmr-verbose` flag that prints module lists to stdout for debugging.

## Validation Plan
- **Automated**: Extend watch integration tests to simulate JS and CSS edits and assert the bridge sends `HotUpdate` instead of `Reload`.
- **Manual**: Smoke test key flows (JS stateful component, CSS tweak, HTML edit) across macOS/Linux/Windows.
- **Performance**: Capture rebuild + apply timings; ensure hot updates stay under 200 ms end-to-end.
- **Fallbacks**: Force a module to return `false` from `__webstirAccept` and verify the system triggers a reload and logs the fallback diagnostic.
- See `docs/how-to/hmr-validation.md` for the end-to-end checklist that captures the scenarios above.

## Risks & Mitigations
- **Module Contract Drift**: Without a framework runtime, inconsistent accept handlers could break pages. Mitigate by providing shared helpers and clear documentation.
- **Race Conditions**: Sequentialize SSE handling and module application within the runtime to avoid interleaved updates.
- **Stale CSS**: Ensure link swaps wait for the `load` event before removing the old node to avoid flash of unstyled content.
- **Diagnostics Overhead**: Keep payloads compact; add sampling if logs become noisy.

## Success Criteria
- Hot updates succeed for JS and CSS edits in the seed project with no full reload.
- Reload fallback occurs automatically for HTML/manifest changes and when modules decline updates.
- HMR coexists with the reload fallback so forced reloads continue to work when required.
- Telemetry confirms <10% fallback rate in typical development sessions once the feature lands.

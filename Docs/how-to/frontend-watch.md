# Frontend Watch Daemon

Guidance for running and troubleshooting the incremental frontend watch workflow.

## Overview
- `webstir watch` launches the daemon and streams rebuild results to the .NET toolchain.
- The browser badge reflects build state: orange for in-progress, green for success, red for errors, and red/gray when disconnected.
- Reload events are debounced so rapid edits coalesce into a single browser refresh.

## CLI Commands
- Default workflow: `webstir watch`
- Manual daemon: `npx webstir-frontend watch-daemon --workspace <absolute-path>`
- Defer auto-start for manual control: add `--no-auto-start`
- Enable verbose diagnostics: add `--verbose`
- Focus on hot-update diagnostics: add `--hmr-verbose`
- Send a manual change event (stdin JSON): `{"type":"change","path":"/absolute/path/to/file"}`
- Request a manual reload: `{"type":"reload"}`
- Shutdown the daemon cleanly: `{"type":"shutdown"}`

## Verbose Logging
- Toggle verbose mode temporarily with the CLI flag (`--verbose`) or set `WEBSTIR_FRONTEND_WATCH_VERBOSE=1` before running `webstir watch`.
- Surface hot-update counters and fallback reasons with `--hmr-verbose` or the `WEBSTIR_FRONTEND_HMR_VERBOSE=1` environment variable.
- Verbose mode surfaces detailed diagnostics (builder timings, esbuild stats, context churn); default mode keeps logs quiet.
- HMR verbose mode emits per-update module/style lists, cumulative hot-update vs. reload totals, and fallback reasons when a reload is required.

## Failure Recovery
1. Restart the daemon: stop `webstir watch` with `Enter`, then run it again.
2. If npm lockfiles drift, delete `package-lock.json` and re-run `npm install`; the watch workflow also falls back automatically when `npm ci` fails.
3. Bypass the daemon with `npx webstir-frontend build --workspace <absolute-path>` for one-off builds.
4. When the badge stays red, inspect the last `frontend.watch` error and re-run with verbose logging if needed.

## Fallbacks
- You can revert to the previous per-invocation CLI flow by running `webstir-frontend build`/`rebuild` manually.
- Clearing `build/frontend` and `dist/frontend` is safe; the daemon will repopulate outputs on the next rebuild.
- Hot-update stats are included in `frontend.watch.pipeline.success` diagnostics and browser console logs so you can confirm fallbacks stay rare (<10%).

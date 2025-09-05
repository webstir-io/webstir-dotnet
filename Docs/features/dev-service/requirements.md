# Requirements: Dev Service

- Scope: file watching, change aggregation, incremental work, dev servers, and ready signaling.

## Customer Requirements (CR)
- Quick feedback loop during development.
- Clear “ready” signal after builds.
- Robustness to file save bursts and transient errors.

## Product Requirements (PR)
- Watch project files with sensible ignores (build/dist/node_modules).
- Debounce rapid changes and coalesce queued work.
- Emit a clear, single "ready" event after successful cycles.
- Manage dev servers (web/node) lifecycle; pick or respect ports.
- Restart gracefully on failures; keep the loop alive.

## Software Requirements (SR)
- `WatchService` monitors relevant folders and produces change events.
- `ChangeService` aggregates events, applies debounce, and queues work.
- `DevService` orchestrates: triggers rebuilds, controls servers, prints "ready".
- Provide hooks for bundling steps to integrate without tight coupling.
- Handle exceptions: log and continue; exit only on fatal configuration errors.

## Acceptance Criteria
- Saving files triggers a single rebuild and a "ready" signal.
- Bursts of saves result in one cycle after debounce.
- Crashes in a cycle do not kill the service; errors are visible.


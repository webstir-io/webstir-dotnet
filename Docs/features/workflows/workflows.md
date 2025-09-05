# Workflows

End‑to‑end flows driven by the CLI. Workflows coordinate workers, pipelines, and services to deliver a complete task (init → build → watch/test → publish). They are orchestration only; low‑level logic lives in engine components.

## What They Are
- Thin coordinators that call workers and services in a defined order.
- Source of truth for user‑visible behavior, exit codes, and logs.
- Deterministic: same inputs produce the same outputs.
- Composable: some workflows run others (e.g., `watch` runs `build` then `test`).

See also: Engine internals — [engine](../solution/engine/engine.md)

## Workflow Docs
- Init — [init](./init/init.md)
- Build — [build](./build/build.md)
- Watch — [watch](./watch/watch.md)
- Test — [test](./test/test.md)
- Publish — [publish](./publish/publish.md)
- Generators — [add‑page](./add-page/add-page.md), [add‑test](./add-test/add-test.md)

## How They Fit In
- CLI selects a workflow (`webstir <command>`), builds an `AppWorkspace`, then invokes the engine.
- Workflows call workers (client/server/shared) and pipelines (HTML/CSS/TS) to do the actual work.
- Long‑running behavior (serving, watching, proxying, reload) is handled by services. See [services](../solution/services/services.md) and [servers](../solution/servers/servers.md).
- All paths and file names come from the workspace and constants. See [workspace](../solution/workspace/workspace.md).

## Initiation
- CLI commands (primary):
  - `webstir init [options] [directory]`
  - `webstir build [--clean]`
  - `webstir watch` (default when no command is provided)
  - `webstir test`
  - `webstir publish`
  - Generators: `webstir add-page <name>`, `webstir add-test <name-or-path>`
- Programmatic chaining: workflows may invoke others as steps (e.g., `watch` → `build` → `test` → start services).
- CI usage: run discrete workflows with standard exit codes to gate builds.

## Responsibilities
- Validate inputs and workspace invariants; fail fast with clear errors.
- Decide order of operations and which workers/services to involve.
- Surface concise, actionable logs and return appropriate exit codes.
- Never hand‑roll path strings; use workspace helpers and constants.

## Lifecycle (By Workflow)
### init
- Create a project from embedded templates (client, server, shared, types).
- Normalize names/paths; write a ready‑to‑run structure.
- Output: scaffolded source tree under `src/**` plus `types/`.

### build
- Compile TypeScript, process CSS, and merge page HTML into `build/`.
- Perform incremental work when possible; keep output readable in dev.
- Output: `build/client/**`, `build/server/**`.

### watch
- Run an initial `build` and `test`.
- Start the dev web server and Node API server; begin watching `src/**`.
- On client change: rebuild affected assets and notify browsers via SSE reload.
- On server change: rebuild server code and restart the Node process.

### test
- Build (if needed) and execute compiled tests in Node.
- Produce a pass/fail summary with CI‑friendly exit codes.

### publish
- Produce optimized, fingerprinted assets in `dist/` per page.
- Minify HTML/CSS/JS, remove comments and source maps, and rewrite HTML links using per‑page manifests.
- Output: `dist/client/pages/<page>/index.html`, `index.<timestamp>.{css|js}`, `manifest.json`.

### generators
- `add-page <name>`: scaffold `index.html|css|ts` under `src/client/pages/<name>/`.
- `add-test <name-or-path>`: scaffold a `.test.ts` in the nearest `tests/`.

## Contracts & Guarantees
- Source roots: `src/client/**`, `src/server/**`, `src/shared/**`, `types/**`.
- Dev outputs: `build/client/**`, `build/server/**`.
- Prod outputs: `dist/client/pages/<page>/**` with per‑page manifests.
- Dev server proxies `/api/*` to the Node process during `watch`.
- Base HTML requires `<main>` in `src/client/app/app.html`.

## Errors & Exit Codes
- Validation failures (missing base HTML, bad flags, TS errors, failing tests) return non‑zero exit codes.
- Logs name the failing stage and file when possible.

## Adding a New Workflow
- Keep it thin: coordinate, don’t implement pipelines in the workflow.
- Use `AppWorkspace` and constants; avoid manual path strings.
- Compose existing workers/services; prefer small, testable steps.
- Define clear inputs, outputs, and exit behavior; add E2E tests that lock down the contract.

## Related Docs
- Solution overview — [solution](../solution/solution.md)
- Engine internals — [engine](../solution/engine/engine.md)
- Services — [services](../solution/services/services.md)
- Servers — [servers](../solution/servers/servers.md)
- Workspace & paths — [workspace](../solution/workspace/workspace.md)
- Testing — [tests](../solution/tests/tests.md) and [.codex/testing.md](../../../.codex/testing.md)

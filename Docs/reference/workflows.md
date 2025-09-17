# Workflows

End-to-end flows driven by the CLI. Workflows coordinate workers, pipelines, and services to deliver a complete task (init → build → watch/test → publish). They are orchestration only; low-level logic lives in engine components.

## What They Are
- Thin coordinators that call workers and services in a defined order.
- Source of truth for user-visible behavior, exit codes, and logs.
- Deterministic: same inputs produce the same outputs.
- Composable: some workflows run others (e.g., `watch` runs `build` then `test`).

See also: Engine internals — [engine](../explanations/engine.md)

## Workflow Docs
- Init — [init](../how-to/init.md)
- Build — [build](../how-to/build.md)
- Watch — [watch](../how-to/watch.md)
- Test — [test](../how-to/test.md)
- Publish — [publish](../how-to/publish.md)
- Generators — [add-page](../how-to/add-page.md), [add-test](../how-to/add-test.md)

## How They Fit In
- CLI selects a workflow (`webstir <command>`), builds an `AppWorkspace`, then invokes the engine.
- Workflows call workers (frontend/backend/shared) and pipelines (HTML/CSS/TS) to do the actual work.
- Long-running behavior (serving, watching, proxying, reload) is handled by services. See [services](../explanations/services.md) and [servers](../explanations/servers.md).
- All paths and file names come from the workspace and constants. See [workspace](../explanations/workspace.md).

### Workers & DI
- Contracts:
  - `IWorkflowWorker`: common worker contract (BuildOrder, InitAsync, BuildAsync, PublishAsync).
  - `IFrontendWorker`: extends `IWorkflowWorker` with `AddPageAsync` for page scaffolding.
- DI pattern: all workers are registered as `IWorkflowWorker` and workflows inject `IEnumerable<IWorkflowWorker>`.
- Filtering: workflows choose which workers to run based on project mode (client-only, server-only, fullstack). The `add-page` workflow resolves the single `IFrontendWorker`, which now shells out to `webstir-frontend add-page` for scaffolding.

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
- Never hand-roll path strings; use workspace helpers and constants.

## Lifecycle (By Workflow)
### init
- Create a project from embedded templates (frontend, backend, shared, types).
- Normalize names/paths; write a ready-to-run structure.
- Output: scaffolded source tree under `src/**` plus `types/`.

### build
- Compile TypeScript with `tsc --build`, then bundle JavaScript with **esbuild** for fast development builds.
- Process CSS imports and merge page HTML into `build/`.
- Copy Images, Fonts, and Media to `build/frontend/{images|fonts|media}/**`.
- Perform incremental work when possible; keep output readable with source maps in dev.
- Output: `build/frontend/**`, `build/backend/**`.

### watch
- Run an initial `build` and `test`.
- Start the dev web server and Node API server; begin watching `src/**`.
- On frontend change: rebuild affected assets and notify browsers via SSE reload.
- On backend change: rebuild backend code and restart the Node process.

### test
- Build (if needed) and execute compiled tests in Node.
- Produce a pass/fail summary with CI-friendly exit codes.

### publish
- Produce optimized, fingerprinted assets in `dist/` per page.
- Use **esbuild** to bundle, tree-shake, minify JavaScript, and perform **automatic code-splitting** with production optimizations.
- esbuild extracts shared dependencies into `chunks/` folder; manages content hashing for entries (`index-<hash>.js`) and chunks (`chunks/*-<hash>.js`).
- Minify HTML/CSS, remove comments and source maps, and rewrite HTML links using per-page manifests.
- Optimize Images and Fonts when tools available, then copy to `dist/frontend/{images|fonts|media}/**`.
- Output: `dist/frontend/pages/<page>/index.html`, `index-<hash>.js`, `index.<hash>.css`, `manifest.json`, plus `dist/frontend/chunks/*-<hash>.js`.

### generators
- `add-page <name>`: scaffold `index.html|css|ts` under `src/frontend/pages/<name>/`.
- `add-test <name-or-path>`: scaffold a `.test.ts` in the nearest `tests/`.

## Contracts & Guarantees
- Source roots: `src/frontend/**`, `src/backend/**`, `src/shared/**`, `types/**`.
- Dev outputs: `build/frontend/**`, `build/backend/**` (including `build/frontend/{images|fonts|media}/**`).
- Prod outputs: `dist/frontend/pages/<page>/**` with per-page manifests, `dist/frontend/chunks/**` for shared code, plus static assets under `dist/frontend/{images|fonts|media}/**`.
- Dev server proxies `/api/*` to the Node process during `watch`.
- Base HTML requires `<main>` in `src/frontend/app/app.html`.

## Errors & Exit Codes
- Validation failures (missing base HTML, bad flags, TS errors, failing tests) return non-zero exit codes.
- Logs name the failing stage and file when possible.

## Adding a New Workflow
- Keep it thin: coordinate, don’t implement pipelines in the workflow.
- Use `AppWorkspace` and constants; avoid manual path strings.
- Compose existing workers/services; prefer small, testable steps.
- Define clear inputs, outputs, and exit behavior; add E2E tests that lock down the contract.

## Related Docs
- Solution overview — [solution](../explanations/solution.md)
- Engine internals — [engine](../explanations/engine.md)
- Services — [services](../explanations/services.md)
- Servers — [servers](../explanations/servers.md)
- Workspace & paths — [workspace](../explanations/workspace.md)
- Testing — [tests](../explanations/testing.md) and [.codex/testing.md](../../.codex/testing.md)

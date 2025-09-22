# Testing

How we test the Webstir solution and what we lock down. This expands the high-level approach in [.codex/testing.md](../../.codex/testing.md) and focuses on the CLI/Engine.

## Overview
- Goal: protect the developer experience, not chase coverage.
- Focus: end-to-end CLI workflows and published outputs.
- Contract first: commands, flags, exit codes, folder structure, manifests, and dev behavior (reload, proxy).

## What We Test
- Workflows: init → build → watch → test → publish. See [workflows](../../workflows/workflows.md).
- Generators: `add-page`, `add-test` create expected files and integrate with builds.
- Contracts: directory layout, build artifacts, per-page `manifest.json`, rewritten HTML links.
- Dev behavior: dev web server serves pages, proxies `/api/*`, live reload via SSE.
- Error paths: invalid flags, missing `<main>` in `app.html`, TypeScript errors, failing tests → non-zero exit.

## What We Don’t Test
- Private helpers, wiring details, or internal classes.
- Line-by-line coverage metrics.
- Transient implementation quirks that don’t affect public behavior.

## Test Types
- End-to-End CLI: run `webstir` commands against a temp workspace and assert outputs.
- Snapshot (golden) tests: compare generated files or folder trees to known-good versions.
- Property tests: invariants like “any project name that matches rules builds”, “page names are normalized consistently”.
- Smoke checks: dev server responds on a port; watch triggers rebuild on file change.
- Error scenario tests: bad input produces clear messages and non-zero exit codes.

## Harness & Structure
- Location: `Tests/` — .NET test harness driving the CLI and validating outputs.
- Isolation: each test runs in a unique temp directory; no global state.
- Process: spawn CLI via the test harness, capture stdout/stderr, and check exit codes.
- Filesystem: assert on existence, contents, and structure of `build/` and `dist/`.

## Snapshots
- Use snapshots to lock down scaffolding and publish outputs.
- Keep snapshots readable; prefer whole-file or folder snapshots over brittle line assertions.
- Normalize dynamic data (timestamps, absolute paths, random IDs) before comparing.
- When a legitimate change occurs, review and update the snapshot intentionally.

## Running Tests
- Repo tests (engine/CLI): run `dotnet test` at the repo root.
- Generated project tests: inside a project created by `webstir init`, run `webstir test` (delegates to the `webstir-test` CLI).
- CI: relies on standard exit codes; non-zero fails the job.

## Writing Tests
- Choose behavior to lock down (a workflow, generator, or contract).
- Arrange: create a temp workspace; optionally run `webstir init` with flags.
- Act: run the target command (`build`, `watch` smoke, `publish`, generator).
- Assert: check exit code, logs for key messages, and the filesystem outputs.
- For snapshots: generate output, normalize dynamic values, compare to the approved snapshot.

## Dev Server & Watch
- Smoke test: after `watch` starts, the web server responds and logs “watching” state.
- Change test: touch a frontend file → expect frontend rebuild + reload signal; touch a backend file → expect backend restart.
- Proxy test: request under `/api/*` routes to the Node server.

## Publish Specifics
- Fingerprinted assets: expect per-page `index.<timestamp>.{css|js}` written to `dist/frontend/pages/<page>/`.
- HTML rewrite: page HTML references fingerprinted assets using the per-page `manifest.json`.
- Minification: HTML/CSS/JS are minified; comments and source maps removed.

## Determinism & Flakiness
- Avoid port conflicts by using OS-assigned ports or a shared allocator.
- Use generous but bounded timeouts; don’t rely on sleeps where event signals exist.
- Normalize any time-based names and absolute paths before assertions.
- Batch file changes to avoid excessive rebuilds in watch tests.

## Related Docs
- Solution overview — [solution](solution.md)
- CLI reference — [cli](../reference/cli.md)
- Engine internals — [engine](engine.md)
- Pipelines — [pipelines](pipelines.md)
- Testing — [tests](testing.md), [.codex/testing.md](../../.codex/testing.md)
- Workspace and paths — [workspace](workspace.md)

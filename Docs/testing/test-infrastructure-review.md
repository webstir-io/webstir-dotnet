# Webstir Test Infrastructure Review

This document summarizes the current state of the test framework and highlights key issues and recommendations to stabilize and extend init/build/publish E2E testing.

## Overview

- Custom test runner in `Tests` executes suites via DI and prints concise summaries.
- Suites exercise CLI commands end-to-end by spawning `dotnet CLI.dll` with timeouts via `ProcessRunner`.
- Existing suites: init, build, watch, publish, help.

## Critical Mismatches

- Init output location mismatch
  - Code seeds into a `seed` subfolder when running in the current directory.
    - Engine/Workflows/InitWorkflow.cs:12
    - Engine/Workflows/BaseWorkflow.cs:42
  - Tests assert files directly under the test directory (not under `seed`).
    - Tests/Suite/InitTests.cs:25
- Watch readiness sentinel mismatch
  - Dev/watch logs never print “Watching for changes”, but tests wait for it.
    - Engine/Services/WatchService.cs:18
    - Engine/Services/DevService.cs:66
  - Tests expect “Watching for changes”.
    - Tests/Suite/WatchTests.cs:29

## Environment & Tooling Assumptions

- Global TypeScript compiler required (`tsc`), and `npm install` may run during build.
  - Engine/Workers/ServerWorker.cs:60
  - Engine/Pipelines/JavaScript/Build/JsBuilder.cs:36
- Risk: CI or dev machines without global `tsc` will fail; `npm install` lengthens builds.
- Node server startup kills any process bound to the API port before starting.
  - Engine/Servers/NodeServer.cs:92

## Timeouts & Flakiness Risks

- Tight timeouts (8–15s) for commands that can exceed these with `npm install` and `tsc`.
  - Tests/Suite/BaseTest.cs:15
  - Tests/Suite/BuildTests.cs:19
  - Tests/Suite/PublishTests.cs:19
  - Tests/Suite/WatchTests.cs:21

## Assertions & Contracts

- Publish test accepts exit code 0 or 1; this weakens contract.
  - Tests/Suite/PublishTests.cs:29
- Named init test title implies a name, but no name is actually provided; no explicit CLI support for named init.
  - Tests/Suite/InitTests.cs:40
  - Engine/Workflows/InitWorkflow.cs:9

## Runner & Process Control

- `ProcessRunner` lacks environment variable injection; tests can’t easily set alternate ports or flags.
  - Tests/Framework/ProcessRunner.cs:14
- SIGINT is sent on Unix to terminate watch; kill tree fallback is implemented for timeouts.

## Minor / Polish

- Help mentions a demo suite, but none is registered.
  - Tests/Program.cs:66
- Compilation error checks ignore stderr; most output is logged, but considering stderr may help.

## Recommendations (Targeted Changes)

1) Align init expectations
- Update init tests to assert under `<testDir>/seed/...` or change `InitWorkflow` to seed at the working path root. Prefer adapting tests to current behavior for minimal diffs.

2) Fix watch readiness detection
- Update `WatchTests` to wait for one of the actual readiness messages:
  - “Started watching for file changes” (WatchService)
  - or “Dev Service is running. Press Ctrl+C to exit.” (DevService)

3) Stabilize environment assumptions
- Prefer local TypeScript compiler: add `typescript` to template devDependencies and invoke `npx tsc` or `node_modules/.bin/tsc` in builders/workers.
- Provide a way to run builds without starting servers during tests or allow setting ports via env.

4) Relax timeouts for E2E
- Build: 30–60s; Publish: 60s; Watch readiness: 15–20s.

5) Tighten contracts
- Require exit code 0 for publish success.
- Either implement “init <name>” / `--project-name` for init or adjust the “named” test to reflect current capabilities.

6) Enhance `ProcessRunner`
- Add optional env var dictionary to `ProcessRunOptions` and apply it to the spawned process to isolate ports and behavior in tests.

## Proposed Next Steps

- Patch tests to align with current behavior (seed folder, watch sentinel, timeouts).
- Add env support to `ProcessRunner` and update tests to set unique high ports to avoid killing local services.
- Optionally: change build/server workers to prefer local `tsc` (`npx tsc`) to reduce global tooling requirements.

If you want, I can implement these surgical changes now, starting with updating the tests and `ProcessRunner` for env overrides.


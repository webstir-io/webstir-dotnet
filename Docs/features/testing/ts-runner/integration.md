# CLI Integration

## Commands
- `webstir test`
  - Runs application tests once and prints a summary.
- `webstir watch`
  - After a successful rebuild, runs tests and prints results inline.

## Flow: `webstir test`
- Resolve workspace (project root).
- Ensure build is up to date (incremental build).
- Discover source tests: `TestDiscovery.FindSourceTests(...)`.
- Map to compiled JS: `TestDiscovery.MapToCompiled(...)`.
- Run tests: `TestRunner.RunAsync(compiledFiles, ct)`.
- Print results summary to console.
- Return success/failure status to the shell.

## Flow: `webstir watch`
- Start existing watch pipeline.
- On successful build, repeat discovery → map → run → print.
- Ignore overlapping runs; queue the latest.

## Touchpoints (proposed)
- `CLI/Help.cs`
  - Add `test` command help entry.
- `Engine/Workflows/TestWorkflow.cs`
  - Implements the `test` workflow, using `TestDiscovery` and `TestRunner`.
- `Engine/Pipelines/Core/Testing/TestDiscovery.cs`
  - `FindSourceTests`, `MapToCompiled`.
- `Engine/Pipelines/Core/Testing/TestRunner.cs`
  - Hosts Node, loads `tester.js`, executes compiled tests, returns results.
- `Engine/Pipelines/Core/Testing/TestResults.cs`
  - `TestResult`, `RunResult` models.
- `Engine/Pipelines/Core/Testing/tester.js`
  - Embedded runner script (globals, registry, executor, reporter).

## Output (MVP)
- Per test: name and status.
- Summary: totals and duration.
- Clear message when no tests are found.

## Errors (MVP)
- Build or load errors are shown with a short message.
- Module evaluation errors are attributed to the file that failed.

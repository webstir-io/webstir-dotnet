# Test

Build the project and execute tests through the bundled `webstir-test` TypeScript CLI. Returns CI-friendly exit codes.

## Purpose
- Validate behavior via end-to-end and smoke tests.
- Keep the public contracts locked down.

## When To Use
- After changes to pipelines or templates.
- In CI to gate merges and releases.

## CLI
- `webstir test`

## Steps
1. Ensure build outputs are up to date (runs `build` if needed).
2. Ensure the embedded `@webstir/test` package is installed into the workspace.
3. Execute compiled frontend and backend tests via the `webstir-test` CLI and stream structured results back to the .NET host.
4. Print pass/fail summary with file names for failures.

## Outputs
- Exit code 0 on success; non-zero on failures.
- Logs with clear failure messages.

## Errors & Exit Codes
- Non-zero on test failures, build failures, or configuration errors.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Tests — [tests](../explanations/testing.md)
- Engine — [engine](../explanations/engine.md)
- Pipelines — [pipelines](../explanations/pipelines.md)

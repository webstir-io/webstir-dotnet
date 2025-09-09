# Test

Build the project and execute tests with a lightweight Node runner. Returns CI-friendly exit codes.

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
2. Execute compiled tests under Node.
3. Print pass/fail summary with file names for failures.

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

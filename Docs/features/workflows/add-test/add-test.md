# Add Test

Scaffold a new `.test.ts` in the nearest `tests/` folder so it runs with the `test` workflow.

## Purpose
- Create a test quickly in the right location.
- Keep tests organized alongside the code under test.

## When To Use
- Adding coverage for a feature, workflow, or contract.

## CLI
- `webstir add-test <name-or-path>`

## Inputs
- `<name-or-path>`: file name or relative path. The workflow resolves the closest `tests/` folder from the provided context.

## Steps
1. Resolve target `tests/` folder.
2. Create the file if it does not exist and write a minimal template.
3. Ensure it is picked up by the Node test runner.

## Outputs
- New test file: `<resolved-tests-folder>/<name>.test.ts`.

## Errors & Exit Codes
- Non‑zero if the path is invalid, the file exists, or file IO fails.

## Related Docs
- Workflows — [workflows](../workflows.md)
- Test — [test](../test/test.md)
- Tests philosophy — [tests](../../solution/tests/tests.md)

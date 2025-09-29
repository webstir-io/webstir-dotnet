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
2. Delegate to the `@electric-coding-llc/webstir-test` TypeScript CLI (`webstir-test-add`) which writes the template if it does not already exist.
3. Ensure the local `@electric-coding-llc/webstir-test` package archive and dependency entry exist so the Node test runner can execute.

## Outputs
- New test file: `<resolved-tests-folder>/<name>.test.ts`.

## Errors & Exit Codes
- Non-zero if the path is invalid, the file exists, or file IO fails.

## Related Docs
- Workflows — [workflows](../reference/workflows.md)
- Test — [test](test.md)
- Tests — [tests](../explanations/testing.md)

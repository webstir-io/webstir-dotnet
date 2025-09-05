# Files and Types

This framework lives inside Webstir. Projects do not contain framework files.

## Ownership and Locations
- Webstir (embedded): runner and APIs bundled as JS text and invoked in a Node process.
- Project (optional): a tiny ambient types file for editor hints.

## Webstir Files (proposed)
- `Engine/Pipelines/Core/Testing/TestRunner.cs`
  - Purpose: Orchestrate discovery, path mapping to compiled JS, process launch, and result collection.
  - Members:
    - `Task<RunResult> RunAsync(IEnumerable<string> compiledFiles, CancellationToken ct)`
    - `Task<RunResult> RunForWorkspaceAsync(AppWorkspace workspace, CancellationToken ct)`

- `Engine/Pipelines/Core/Testing/TestDiscovery.cs`
  - Purpose: Find source tests and map them to compiled outputs.
  - Members:
    - `IEnumerable<string> FindSourceTests(AppWorkspace workspace)`
    - `IEnumerable<string> MapToCompiled(IEnumerable<string> sourceTests, AppWorkspace workspace)`

- `Engine/Pipelines/Core/Testing/TestResults.cs`
  - Purpose: Result models.
  - Members:
    - `record TestResult(string Name, string File, bool Passed, string? Message, long DurationMs)`
    - `record RunResult(int Passed, int Failed, int Total, long DurationMs, IReadOnlyList<TestResult> Results)`

- `Engine/Pipelines/Core/Testing/tester.js` (embedded resource)
  - Purpose: The entire TS test runner in one JS file, injected into Node with `-e`.
  - Exposed function:
    - `async function run(files /* string[] */) /* returns RunResult JSON */`
  - Internal modules (in-file):
    - `test(name, fn)` — registers a test on `globalThis`.
    - `assert.isTrue(value, message?)`, `assert.equal(expected, actual, message?)`, `assert.fail(message)`.
    - `registry` — collects tests per module.
    - `executor` — runs tests sequentially and captures results.
    - `reporter` — formats console output (MVP: minimal).

## Project Types (optional)
- `types/webstir/index.d.ts`
  - Purpose: Editor type hints so `test` and `assert` compile (no runtime code).
  - Shape:
    - `export {};`
    - `declare global {`
    - `  function test(name: string, fn?: () => unknown | Promise<unknown>): void;`
    - `  namespace assert {`
    - `    function isTrue(value: unknown, message?: string): void;`
    - `    function equal<T>(expected: T, actual: T, message?: string): void;`
    - `    function fail(message: string): never;`
    - `  }`
    - `}`

### TS Config Integration
- Preferred: Add to root `base.tsconfig.json`:
  - `compilerOptions.typeRoots = ["./types", "./node_modules/@types"]`
- The `add-test` command creates `types/webstir/index.d.ts` if missing and updates `base.tsconfig.json` accordingly.

## Path Mapping (source → compiled)
- Source: `src/client/**/tests/**/*.ts` → Compiled: `build/client/**/tests/**/*.js`
- Source: `src/server/**/tests/**/*.ts` → Compiled: `build/server/**/tests/**/*.js`
- Source: `src/shared/**/tests/**/*.ts` → Compiled: `build/shared/**/tests/**/*.js`

Mapping rule: Replace the first segment `src/<area>/` with `build/<area>/` and change `.ts` to `.js`.

# TypeScript Test Framework Architecture

## Purpose
- Minimal, zero‑dependency framework to run tests authored in TypeScript.
- Embedded in Webstir (no framework files added to projects).
- Sequential execution with clear, structured results.

## Components
- Embedded API: `test` and `assert`
  - Responsibility: Define tests and perform basic assertions via globals.
  - Behavior: The runner injects `globalThis.test` and `globalThis.assert` before loading compiled test modules.
  - Supports: SR-01, SR-02.

- Registry
  - Responsibility: Collect tests declared during module evaluation; drain between modules.
  - Inputs: Calls from the embedded `test` API.
  - Outputs: Array of test cases for the Executor.
  - Supports: SR-03.

- Module Loader (compiled JS)
  - Responsibility: Load compiled test modules by file path from the build output and let them register tests.
  - Inputs: Ordered list of `build/**/tests/**/*.js` file paths.
  - Outputs: Per‑module collected tests, or a module‑level failure if evaluation fails.
  - Supports: SR-03, SR-06.

- Executor
  - Responsibility: Run tests sequentially; capture pass/fail, message, and duration.
  - Inputs: Test cases from Registry.
  - Outputs: Per‑test results.
  - Supports: SR-04, SR-06.

- Reporter
  - Responsibility: Present per‑test status and a concise summary.
  - Inputs: Test results and totals.
  - Outputs: Console output.
  - Supports: SR-06.

- Runner (hosted by Webstir)
  - Responsibility: Orchestrate Loader → Registry → Executor → Reporter; expose a programmatic entry point; runs inside a Node process started by Webstir.
  - Inputs: List of compiled test file paths.
  - Outputs: Structured run result (no process exit, no file writes to project).
  - Supports: SR-07, SR-09, SR-10.

## Data Models
- TestCase
  - Fields: `name: string`, `file: string`, `fn?: () => unknown | Promise<unknown>`.
- TestResult
  - Fields: `name: string`, `file: string`, `status: "passed" | "failed"`, `message?: string`, `durationMs: number`.
- RunResult
  - Fields: `results: TestResult[]`, `passed: number`, `failed: number`, `total: number`, `durationMs: number`.
- Supports: SR-05, SR-06.

## Control Flow
1. Webstir discovers source tests in `src/**/tests/**/*.ts` and maps them to compiled files under `build/**/tests/**/*.js`.
2. Webstir starts Node with the embedded runner and passes the ordered list of compiled files.
3. For each file, the runner loads the module; `test(...)` calls populate the Registry. Module load errors are reported as a failed result for that file. (SR-03, SR-06)
4. Executor runs collected tests sequentially; captures status, message (on failure), and duration. (SR-04, SR-06)
5. Reporter prints per‑test status and a summary; Runner returns RunResult to Webstir. (SR-06, SR-07)

## Error Handling
- Any thrown exception or rejected promise in a test body marks the test as failed with the error message. (SR-06)
- Module evaluation errors are attributed to the file and reported as a failed result. (SR-03)

## Non‑Functional Constraints
- No third‑party packages. (SR-09)
- Deterministic behavior: stable module order; no parallel execution. (SR-04)
- No framework files written to the project; tests rely on existing build outputs. (SR-07)

## Editor Types (Optional)
- Provide ambient declarations via `types/w-test.d.ts` for `test` and `assert`.
- `base.tsconfig.json` includes `compilerOptions.typeRoots = ["./types", "./node_modules/@types"]`.
- `add-test` scaffolds the file and updates config if missing.

## Out of Scope (MVP)
- Suites/grouping, hooks, mocks, snapshots, coverage, parallelism, watch mode, and plugins. (SR-10)

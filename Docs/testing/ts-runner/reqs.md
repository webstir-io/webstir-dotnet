# Customer Requirements
- A TypeScript testing framework for running tests written in TypeScript
- All written from scratch, no 3rd-party dependencies
- MVP, only include the functionality we need to process the TypeScript tests

## Product Requirements
- PR-01 — TypeScript-first authoring
  - Tests are written in TypeScript and run without extra setup.

- PR-02 — Zero-dependency
  - Built from scratch with no third‑party packages.

- PR-03 — Minimal test definition
  - Provide a simple way to define a named test with an optional async body.

- PR-04 — Basic assertions
  - Include a small set of assertions sufficient to express pass/fail conditions.

- PR-05 — Sequential execution
  - Run tests one after another; failures do not stop remaining tests.

- PR-06 — Clear results
  - Show which tests passed or failed and include failure messages.

- PR-07 — Error handling
  - Treat uncaught errors in a test as failures and report them clearly.

- PR-08 — MVP scope only
  - Exclude suites/grouping, hooks, mocking, snapshots, coverage, parallelism, watch mode, and plugins.

- PR-09 — Type-safe developer experience
  - Provide helpful type information and readable failure messages.

## Software Requirements
- SR-01 — Test definition API
  - Shall provide a function to define a named test with an optional async body.
  - Supports: PR-01, PR-03

- SR-02 — Assertion API
  - Shall provide truthiness and equality assertions that mark a test as failed with a message when the condition is not met.
  - Supports: PR-04

- SR-03 — Test module evaluation
  - Shall load and evaluate compiled JavaScript test modules given file paths and collect declared tests.
  - Inputs: list of compiled `.js` test module file paths.
  - Outputs: collected test cases for execution.
  - Errors: module evaluation errors are reported as failures associated with the file.
  - Supports: PR-01, PR-06, PR-07

- SR-04 — Execution semantics
  - Shall execute collected tests sequentially and continue after failures.
  - Outputs: per‑test status and duration.
  - Supports: PR-05, PR-06

- SR-05 — Result model
  - Shall produce structured results including test name, file path, status (passed/failed), optional message, and duration, plus total counts.
  - Supports: PR-06

- SR-06 — Error handling
  - Shall treat thrown exceptions or rejected promises in a test body as failures and include the error message in results.
  - Supports: PR-07

- SR-07 — Runner interface
  - Shall expose a programmatic entry that accepts compiled test module paths and returns structured results; it shall not exit the process or write files.
  - Supports: PR-06, PR-05

- SR-08 — Type information
  - Shall provide TypeScript type definitions for test authoring and assertions to enable editor hints.
  - Supports: PR-09

- SR-09 — Zero‑dependency
  - Shall use no third‑party packages.
  - Supports: PR-02

- SR-10 — MVP boundaries
  - Shall not include suites/grouping, hooks, mocks, snapshots, coverage, parallel execution, watch mode, or plugins.
  - Supports: PR-08

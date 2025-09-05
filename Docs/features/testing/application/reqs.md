# Feature
Integrated, automated testing system.

## Customer Requirements
- A zero-config system
- Auto-detection of test files that are located in folders named "tests" located anywhere under **src**
- A command to add scaffolded tests
- Basic pass/fail results with failure information
- Auto-triggered when a change is detected when running the **watch** command

## Product Requirements
- PR-01 — One‑command test run with no setup.
  - Acceptance: Running `webstir test` in a project executes tests and shows a summary.

- PR-02 — Auto‑discovery of tests in `src/**/tests/`.
  - Acceptance: A test placed under any `tests` folder within `src` is found and run.

- PR-03 — Watch mode runs tests after relevant changes.
  - Acceptance: While `webstir watch` is running, editing a source or test file triggers tests and displays results.

- PR-04 — Basic results with failure details.
  - Acceptance: Output clearly indicates passed/failed tests and shows failure messages and locations.

- PR-05 — Scaffold a starter test via command.
  - Acceptance: Running `webstir add-test <name>` creates a starter test in an appropriate `tests` folder (creating it if needed). Running `webstir add-test <path>` treats `<path>` as relative to `src` and creates `<path>/tests/<name>.test.ts`.
  - Example: From the project root or `src`, `webstir add-test client/app/pages/home/sometest` creates `src/client/app/pages/home/tests/sometest.test.ts`.

## Software Requirements
- SR-01 — Test command
  - Provides a `test` command that runs tests in the current project and prints a summary of results.
  - Supports: PR-01, PR-04

- SR-02 — Discovery scope and patterns
  - Recursively scan `src/**/tests/` for test files.
  - Match files named `*.test.ts` and `*.test.js`.
  - Exclude `node_modules`, `build`, `dist`, and hidden folders.
  - Process files in a deterministic (lexicographic) order.
  - Supports: PR-02

- SR-03 — Execution rules
  - Run tests one by one; a failing test does not stop remaining tests from running.
  - Tests can be synchronous or asynchronous.
  - Supports: PR-01, PR-04

- SR-04 — Result reporting
  - For each test, record pass/fail status.
  - On failure, show the test name, file path, and failure message.
  - After execution, print totals for passed and failed tests.
  - Supports: PR-04

- SR-05 — Watch integration
  - While `watch` is running, re-run tests after a successful build when files under `src/**` change (including tests).
  - Display results in the watch output stream.
  - Supports: PR-03

- SR-06 — Test scaffolding
  - Provide an `add-test <name-or-path>` command.
  - If given a simple name, place it in the nearest `src/**/tests/` folder; if none exists, create `src/tests/`.
  - If given a path under `src` (e.g., `client/app/pages/home/sometest`), create `src/<path>/tests/<name>.test.ts`, creating folders as needed.
  - Use the default filename `<name>.test.ts` and include a minimal passing example.
  - Supports: PR-05

- SR-07 — Zero‑config behavior
  - No configuration files are required to discover or run tests.
  - Default behaviors must work out of the box in a standard project layout.
  - Supports: PR-01, PR-02, PR-03

  

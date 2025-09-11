# Webstir Testing Philosophy

## Guiding Principle
Webstir’s tests exist to **protect the developer experience**, not to exhaustively check every line of code.  
We care about what users see and touch — CLI commands, generated projects, builds, and runtime behavior — not the private details of how those are implemented.

---

## What We Value

### 1. **Workflows over Units**
- We test **end-to-end workflows** (init → build → run) instead of micro units.
- If the *public behavior works*, the internal wiring doesn’t matter.
- This keeps tests meaningful and reduces brittleness when refactoring.

### 2. **Contracts over Coverage**
- The CLI and generated project files form a **contract with users**.
- Tests lock down these contracts: commands, flags, exit codes, directory structure, build results.
- We don’t chase artificial coverage metrics.

### 3. **Snapshots over Micromanagement**
- For scaffolding, we prefer **snapshot (golden master) testing** to verify whole outputs.
- If generated files match known-good versions, we’re confident the system works.

### 4. **Properties over Examples**
- Where possible, we test **invariants** (“any project name should compile”) instead of endless individual cases.
- This catches more real-world issues with fewer tests.

### 5. **Observability as Safety Net**
- We accept that not all bugs can be prevented pre-release.
- Runtime checks, logs, and telemetry are part of the testing strategy.
- Errors in real-world use should be **visible, actionable, and recoverable**.

---

## What We Avoid
- **Test-Driven Development (TDD):** We don’t write tests first. Design comes from exploration, tests come after to capture the stable behaviors.
- **Unit Test Overload:** We don’t cover every function. Most internals can change freely without tests.
- **Coverage Fetish:** A number like 80% coverage tells us nothing about developer happiness. We measure test value in **confidence, not percentages**.

---

## Scope
- **Must Test:** Core workflows, contracts, scaffolding outputs, key invariants.  
- **May Test:** Performance characteristics, optional integrations, rare edge cases.  
- **Won’t Test:** Private helpers, transient details, implementation quirks.  

---

## Philosophy in One Line
> *Test the experience, not the implementation.*

---

## Running Tests

- Quick run (default suites: init, build, publish):
  - `dotnet run --project Tests`

- Full suite (adds watch, help, add):
  - `dotnet run --project Tests -- --full`
  - Or set env: `WEBSTIR_TEST_MODE=full`

- Run a single suite:
  - `dotnet run --project Tests -- test init`
  - `dotnet run --project Tests -- test build`
  - `dotnet run --project Tests -- test publish`
  - `dotnet run --project Tests -- test watch`
  - `dotnet run --project Tests -- test help`
  - `dotnet run --project Tests -- test add`

- See runner help:
  - `dotnet run --project Tests -- help`

Notes
- Requires .NET 9 SDK. Some tests invoke Node/tsc; ensure they’re on PATH.
- The repo doesn’t use xUnit/NUnit; `dotnet test` will not execute this custom runner.

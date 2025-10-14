# Utilities

Helper scripts to keep the repo tidy, build-ready, and easy to demo. These live under `utilities/` and are safe to run from the repo root.

## Scripts

### format.sh
- Purpose: Run `dotnet format` across the solution.
- Usage:
  - `./utilities/format.sh`
  - `./utilities/format.sh --verify-no-changes --severity info`
- Notes: Passes flags through to `dotnet format`. Use in CI to verify style.

### format-build.sh
- Purpose: Fix whitespace, run style/analyzer formatters, build the solution, and execute frontend package tests.
- Usage: `./utilities/format-build.sh`
- Steps:
  - Normalizes `.cs` files and runs `dotnet format whitespace`.
  - `dotnet format style` and `dotnet format analyzers`.
  - `dotnet build Webstir.sln -v minimal`.
  - Installs frontend npm dependencies (`npm ci`) and runs the package tests.
- Tip: Use before committing or when CI fails style checks.

### deploy-seed.sh
- Purpose: Create, build, test, and publish a seed project for demos and the Sandbox.
- Usage: `./utilities/deploy-seed.sh`
- Output directories:
  - Seed root: `CLI/out/seed`
  - Build artifacts: `CLI/out/seed/build`
  - Dist artifacts: `CLI/out/seed/dist`
- Behavior:
  - `init` a fresh seed at `CLI/out/seed` (replaces if exists).
  - `build` with the CLI.
  - `test`; if tests pass, `publish`.
- Works with: Docker Sandbox setup mounting the seed paths.

## Prerequisites
- .NET SDK installed (`dotnet --info`).
- Repo restored and buildable.
- For Sandbox usage of `deploy-seed.sh`, Docker/Compose installed.

## Tips
- Run from the repo root to ensure relative paths resolve correctly.
- Add `format.sh --verify-no-changes` to CI to enforce style.
- If a formatter fails unexpectedly, rerun `./utilities/format-build.sh` to normalize whitespace before `format.sh`.

## Related Docs
- Solution overview — [solution](../explanations/solution.md)
- Engine internals — [engine](../explanations/engine.md)
- CLI reference — [cli](../reference/cli.md)
- Sandbox — [sandbox](sandbox.md)
- Testing — [tests](../explanations/testing.md)

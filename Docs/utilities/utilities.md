# Utilities

Miscellaneous productivity scripts for working in this repo. These are not product features.

## Scripts
- `scripts/format.sh`: Run `dotnet format` with any extra flags you pass through.
- `scripts/format-build.sh`: Fix whitespace, run analyzers, then build the solution.
- `scripts/fix-whitespace.sh`: Normalize newlines, trim trailing spaces, ensure final newlines.
- `scripts/deploy-seed.sh`: Init → build → test → publish the seed app into `CLI/out/seed/**`.

## Notes
- Scripts run from the repo root and use `set -euo pipefail`.
- `deploy-seed.sh` skips publish if tests fail.


#!/usr/bin/env bash
set -euo pipefail

# Initialize, build, and publish the seed project in one go.
# Output paths:
# - Seed:            CLI/out/seed
# - Build artifacts: CLI/out/seed/build
# - Dist artifacts:  CLI/out/seed/dist

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

SEED_DIR="CLI/out/seed"

echo "[1/4] Initializing seed at $SEED_DIR ..."
rm -rf "$SEED_DIR"
dotnet run --project CLI -- init "$SEED_DIR"

echo "[2/4] Building seed ..."
dotnet run --project CLI -- build --project-name "$SEED_DIR"

echo "[3/4] Running tests ..."
if dotnet run --project CLI -- test --project-name "$SEED_DIR"; then
  echo "[4/4] Publishing seed ..."
  dotnet run --project CLI -- publish --project-name "$SEED_DIR"
  echo "Done."
else
  echo "Tests failed; skipping publish." >&2
  echo "Done. Build: $SEED_DIR/build  Dist: (publish skipped)"
  exit 0
fi

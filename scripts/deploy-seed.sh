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

echo "[1/3] Initializing seed at $SEED_DIR ..."
rm -rf "$SEED_DIR"
dotnet run --project CLI -- init "$SEED_DIR"

echo "[2/3] Building seed ..."
dotnet run --project CLI -- build --project-name "$SEED_DIR"

echo "[3/3] Publishing seed ..."
dotnet run --project CLI -- publish --project-name "$SEED_DIR"

echo "Done. Build: $SEED_DIR/build  Dist: $SEED_DIR/dist"


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
if [ -d "$SEED_DIR" ]; then
  if command -v chflags >/dev/null 2>&1; then
    chflags -R nouchg "$SEED_DIR" 2>/dev/null || true
  fi
  if command -v chmod >/dev/null 2>&1; then
    chmod -RN "$SEED_DIR" 2>/dev/null || true
    chmod -R u+w "$SEED_DIR" 2>/dev/null || true
  fi
  if command -v xattr >/dev/null 2>&1; then
    xattr -dr com.apple.provenance "$SEED_DIR" 2>/dev/null || true
  fi
  rm -rf "$SEED_DIR"
fi
dotnet run --project CLI -- init "$SEED_DIR"

echo "[2/3] Running tests ..."
if dotnet run --project CLI -- test --project-name "$SEED_DIR"; then
  echo "[3/3] Publishing seed ..."
  dotnet run --project CLI -- publish --project-name "$SEED_DIR"
  echo "Done."
else
  echo "Tests failed; skipping publish." >&2
  echo "Done. Build: $SEED_DIR/build  Dist: (publish skipped)"
  exit 0
fi

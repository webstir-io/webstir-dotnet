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

echo "[0/4] Updating local package tarballs ..."
"$ROOT_DIR/utilities/build-frontend-package.sh"
"$ROOT_DIR/utilities/build-test-package.sh"

echo "[1/4] Initializing seed at $SEED_DIR ..."
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

echo "[2/4] Installing seed npm dependencies ..."
pushd "$SEED_DIR" >/dev/null
npm install --silent
popd >/dev/null

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

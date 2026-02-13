#!/usr/bin/env bash
set -euo pipefail

# Initialize, build, and publish the seed project in one go.
# Output paths:
# - Seed:            CLI/out/seed
# - Build artifacts: CLI/out/seed/build
# - Dist artifacts:  CLI/out/seed/dist

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

DEFAULT_NPMRC="$ROOT_DIR/.npmrc"
if [[ -n "${NPM_CONFIG_USERCONFIG:-}" ]]; then
  if [[ ! -f "$NPM_CONFIG_USERCONFIG" && -f "$DEFAULT_NPMRC" ]]; then
    echo "deploy-seed: NPM_CONFIG_USERCONFIG points to '$NPM_CONFIG_USERCONFIG' but it does not exist; using $DEFAULT_NPMRC instead." >&2
    export NPM_CONFIG_USERCONFIG="$DEFAULT_NPMRC"
  fi
elif [[ -f "$DEFAULT_NPMRC" ]]; then
  export NPM_CONFIG_USERCONFIG="$DEFAULT_NPMRC"
fi

if [[ -n "${NPM_TOKEN:-${NODE_AUTH_TOKEN:-}}" ]]; then
  npm config set //registry.npmjs.org/:_authToken "${NPM_TOKEN:-${NODE_AUTH_TOKEN:-}}" --location=user >/dev/null 2>&1 || true
fi

SEED_DIR="CLI/out/seed"

echo "[0/5] Updating framework packages (changed only) ..."
dotnet run --project Framework/Framework.csproj -- packages sync --changed-only

echo "[1/5] Initializing seed at $SEED_DIR ..."
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

echo "[2/5] Synchronizing framework packages ..."
dotnet run --project CLI -- install "$SEED_DIR"

echo "[3/5] Running tests ..."
if dotnet run --project CLI -- test "$SEED_DIR"; then
  echo "[4/5] Publishing seed ..."
  dotnet run --project CLI -- publish "$SEED_DIR"
  echo "Done."
else
  echo "Tests failed; skipping publish." >&2
  echo "Done. Build: $SEED_DIR/build  Dist: (publish skipped)"
  exit 0
fi

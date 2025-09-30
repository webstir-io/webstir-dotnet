#!/usr/bin/env bash
set -euo pipefail

show_help() {
  cat <<'USAGE'
Usage: utilities/build-packages.sh [--force] [build-options]

Detects changes under framework/frontend or framework/testing. If any tracked
changes are found (staged or unstaged), the script runs:
  dotnet run --project FrameworkCLI -- build [build-options]

Options:
  --force   Run the build command even when no changes are detected.
  -h|--help Show this help text.

Any additional arguments are passed straight to `framework build`.
USAGE
}

ROOT_DIR=$(git rev-parse --show-toplevel 2>/dev/null || pwd)
cd "$ROOT_DIR"

FORCE=false
SYNC_ARGS=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --force)
      FORCE=true
      shift
      ;;
    -h|--help)
      show_help
      exit 0
      ;;
    *)
      SYNC_ARGS+=("$1")
      shift
      ;;
  esac
done

if [[ "$FORCE" != "true" ]]; then
  if git status --porcelain=v1 -- framework/frontend framework/testing | grep -q '.'; then
    FORCE=true
  fi
fi

if [[ "$FORCE" != "true" ]]; then
  echo "[build-packages] No framework package changes detected. Skipping rebuild."
  exit 0
fi

echo "[build-packages] Detected framework package changes; rebuilding packages." >&2
dotnet run --project FrameworkCLI -- build "${SYNC_ARGS[@]}"

if git ls-files --error-unmatch framework/out/manifest.json >/dev/null 2>&1; then
  if git status --porcelain=v1 framework/out/manifest.json | grep -q '.'; then
    git add framework/out/manifest.json
    echo "[build-packages] Staged framework/out/manifest.json." >&2
  fi
fi

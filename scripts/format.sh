#!/usr/bin/env bash
set -euo pipefail

# Run dotnet format from repo root.
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

# Pass through any extra flags, e.g. --verify-no-changes --severity info
dotnet format --no-restore "$@"


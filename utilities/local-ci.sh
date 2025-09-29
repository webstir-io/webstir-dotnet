#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

if [ -f .env.local ]; then
  # shellcheck disable=SC1091
  source .env.local
fi

if [ -z "${GH_PACKAGES_TOKEN:-}" ]; then
  echo "warning: GH_PACKAGES_TOKEN is not set; npm auth against GitHub Packages may fail" >&2
fi

echo "[local-ci] Building solution"
dotnet build Webstir.sln -v minimal

echo "[local-ci] Running .NET workflow tests"
dotnet run --project Tests

echo "[local-ci] Installing frontend dependencies"
npm ci --silent --prefix framework/frontend

echo "[local-ci] Running frontend package tests"
npm test --prefix framework/frontend

echo "[local-ci] Verifying toolchain artifacts"
dotnet run --project CLI -- toolchain verify

echo "[local-ci] Done."

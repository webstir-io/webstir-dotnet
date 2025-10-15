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

echo "[local-ci] Installing frontend dependencies"
npm ci --silent --prefix Framework/Frontend

echo "[local-ci] Running frontend package tests"
npm test --prefix Framework/Frontend --silent

echo "[local-ci] Installing testing package dependencies"
npm ci --silent --prefix Framework/Testing

echo "[local-ci] Building solution"
dotnet build Webstir.sln -v minimal

echo "[local-ci] Running .NET workflow tests"
dotnet run --project Tests -- --full

echo "[local-ci] Building framework packages (changed only)"
dotnet run --project Framework/Framework.csproj -- packages sync --changed-only

echo "[local-ci] Verifying framework packages"
dotnet run --project Framework/Framework.csproj -- packages verify --all

echo "[local-ci] Dry-run publish pipeline"
dotnet run --project Framework/Framework.csproj -- packages publish --dry-run --changed-only

echo "[local-ci] Done."

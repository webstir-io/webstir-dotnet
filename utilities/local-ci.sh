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

export NODE_AUTH_TOKEN="${GH_PACKAGES_TOKEN:-}"

run_package_repo() {
  local repo="$1"
  local path="../$repo"

  if [ ! -d "$path" ]; then
    echo "[local-ci] Skipping $repo (directory not found at $path)"
    return
  fi

  echo "[local-ci][$repo] npm ci"
  (cd "$path" && npm ci --silent)

  echo "[local-ci][$repo] npm run build"
  (cd "$path" && npm run build --silent)

  if (cd "$path" && node -e "const pkg=require('./package.json'); process.exit(pkg?.scripts?.test ? 0 : 1);"); then
    echo "[local-ci][$repo] npm test"
    (cd "$path" && npm test --silent)
  fi
}

echo "[local-ci] Running contract package checks"
run_package_repo "module-contract"
run_package_repo "testing-contract"

echo "[local-ci] Running package repository checks"
run_package_repo "webstir-frontend"
run_package_repo "webstir-backend"
run_package_repo "webstir-testing"

echo "[local-ci] Installing frontend dependencies"
npm ci --silent --prefix Framework/Frontend

echo "[local-ci] Running frontend package tests"
npm test --prefix Framework/Frontend --silent

echo "[local-ci] Installing testing package dependencies"
npm ci --silent --prefix Framework/Testing

echo "[local-ci] Building solution"
dotnet build Webstir.sln -v minimal

echo "[local-ci] Running .NET workflow tests"
WEBSTIR_TEST_MODE=full dotnet test Tester/Tester.csproj

echo "[local-ci] Building framework packages (changed only)"
dotnet run --project Framework/Framework.csproj -- packages sync --changed-only

echo "[local-ci] Verifying framework packages"
dotnet run --project Framework/Framework.csproj -- packages verify --all

echo "[local-ci] Dry-run publish pipeline"
dotnet run --project Framework/Framework.csproj -- packages publish --dry-run --changed-only

echo "[local-ci] Done."

#!/usr/bin/env bash
set -euo pipefail

# Format and then build the solution from repo root.
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "Fixing whitespace..."
"${ROOT_DIR}/utilities/fix-whitespace.sh"

echo "Running dotnet format (style + analyzers)..."
dotnet format style --no-restore
dotnet format analyzers --no-restore

echo "Checking whitespace (verify only)…"
dotnet format whitespace --no-restore --verify-no-changes || true

echo "Building solution..."
dotnet build Webstir.sln -v minimal

echo "Running frontend package tests..."
pushd "$ROOT_DIR/framework/frontend" > /dev/null
npm ci --silent
npm test
popd > /dev/null

echo "Building framework packages..."
dotnet run --project "$ROOT_DIR/framework/Framework.csproj" -- packages sync

echo "Verifying framework packages..."
dotnet run --project "$ROOT_DIR/framework/Framework.csproj" -- packages verify

echo "Done."

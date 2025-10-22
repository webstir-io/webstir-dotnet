#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$PROJECT_ROOT"

dotnet build

echo "Running full test suite..."

WEBSTIR_TEST_MODE=full dotnet test Tester/Tester.csproj \
  --nologo \
  --logger "console;verbosity=minimal;summary=true" \
  | perl -ne 'print unless /\[xUnit\.net .* (Discovering|Discovered|Starting):/'

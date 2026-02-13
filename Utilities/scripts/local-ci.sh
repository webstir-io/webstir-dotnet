#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
PARENT_DIR="$(dirname "$ROOT_DIR")"
ROOT_NAME="$(basename "$ROOT_DIR")"
IMAGE_NAME="${LOCAL_CI_IMAGE_NAME:-webstir-local-ci:latest}"

configure_npm_auth() {
  if [[ "${WEBSTIR_SKIP_NPM_AUTH:-}" = "1" ]]; then
    return 0
  fi

  if [[ -z "${NPM_TOKEN:-${NODE_AUTH_TOKEN:-}}" ]]; then
    return 0
  fi

  if ! command -v npm >/dev/null 2>&1; then
    echo "[local-ci] warning: npm is not available to configure npmjs auth." >&2
    return 1
  fi

  local token="${NPM_TOKEN:-${NODE_AUTH_TOKEN:-}}"
  npm config set //registry.npmjs.org/:_authToken "$token" --location=user >/dev/null 2>&1
}

# Try to load credentials from .env.local when running on the host.
if [[ -z "${NPM_TOKEN:-}" && -f "$ROOT_DIR/.env.local" ]]; then
  # shellcheck disable=SC1091
  source "$ROOT_DIR/.env.local"
fi

configure_npm_auth >/dev/null 2>&1 || true

if [[ -z "${LOCAL_CI_IN_CONTAINER:-}" ]]; then
  if ! command -v docker >/dev/null 2>&1; then
    echo "[local-ci] error: docker is required to mirror the GitHub Actions environment." >&2
    exit 1
  fi

  echo "[local-ci] Building container image ($IMAGE_NAME)..."
  docker build -f "$ROOT_DIR/utilities/docker/ci.Dockerfile" -t "$IMAGE_NAME" "$ROOT_DIR"

  echo "[local-ci] Running CI workflow inside container..."
  docker_args=(
    run
    --rm
    -e LOCAL_CI_IN_CONTAINER=1
    -e NPM_TOKEN="${NPM_TOKEN:-}"
    -e NODE_AUTH_TOKEN="${NODE_AUTH_TOKEN:-${NPM_TOKEN:-}}"
    -e WEBSTIR_FRONTEND_REGISTRY_SPEC="${WEBSTIR_FRONTEND_REGISTRY_SPEC:-}"
    -e WEBSTIR_TEST_REGISTRY_SPEC="${WEBSTIR_TEST_REGISTRY_SPEC:-}"
    -e WEBSTIR_BACKEND_REGISTRY_SPEC="${WEBSTIR_BACKEND_REGISTRY_SPEC:-}"
    -e WEBSTIR_WRITE_WORKSPACE_NPMRC="${WEBSTIR_WRITE_WORKSPACE_NPMRC:-}"
    -v "$PARENT_DIR":/workspaces
    -w "/workspaces/$ROOT_NAME"
  )

  docker_args+=(
    "$IMAGE_NAME"
    bash
    -lc
    "./utilities/scripts/local-ci.sh"
  )

  docker "${docker_args[@]}"
  exit 0
fi

# From this point onward we are inside the container (Debian-based like GitHub Actions)
# To avoid polluting the host's bin/obj (which can confuse IDE design-time builds),
# run the workflow against an isolated copy of the repo inside the container.

CI_WORK_DIR="/tmp/webstir-ci"
rm -rf "$CI_WORK_DIR"
mkdir -p "$CI_WORK_DIR"
cp -R . "$CI_WORK_DIR"
cd "$CI_WORK_DIR"

step() {
  echo "[local-ci] $*"
}

configure_npm_auth >/dev/null 2>&1 || true

run_in() {
  local dir="$1"
  shift
  (
    cd "$dir"
    "$@"
  )
}

run() {
  if ! "$@"; then
    echo "[local-ci] command failed: $*" >&2
    exit 1
  fi
}

# Ensure npm has the npmjs token when provided.
if [[ -n "${NPM_TOKEN:-}" ]]; then
  step "Configure npm auth for npmjs"
  configure_npm_auth >/dev/null 2>&1 || {
    echo "[local-ci] warning: failed to configure npm auth; continuing" >&2
  }
fi

step "Install workspace dependencies (bun install --frozen-lockfile)"
run rm -rf node_modules Framework/*/node_modules || true
run bun install --frozen-lockfile

step "Build module contract package"
run bun run --cwd Framework/Contracts/module-contract build
step "Build backend package"
run bun run --cwd Framework/Backend build
step "Build frontend package"
run bun run --cwd Framework/Frontend build

step "Build testing package (bun run --cwd Framework/Testing build)"
run bun run --cwd Framework/Testing build

step "Clear NuGet caches"
run dotnet nuget locals all --clear >/dev/null

step "dotnet build Webstir.sln"
run dotnet build Webstir.sln -v minimal

step "Run .NET workflow tests (WEBSTIR_TEST_MODE=full dotnet test)"
WEBSTIR_TEST_MODE=full run dotnet test Tester/Tester.csproj --nologo --logger "console;verbosity=minimal;summary=true"

step "Run frontend package tests (bun run --cwd Framework/Frontend test)"
run bun run --cwd Framework/Frontend test

step "Build framework packages (dotnet run -- packages sync)"
run dotnet run --project Framework/Framework.csproj -- packages sync

step "Completed GitHub CI equivalent workflow."

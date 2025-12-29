#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/publish.sh <patch|minor|major|x.y.z> [--no-push]

Examples:
  scripts/publish.sh patch
  scripts/publish.sh 0.1.0

The script requires a clean git worktree and an npm login to
https://npm.pkg.github.com with write:packages access.

By default, the script pushes the version bump commit and tag. To skip pushing,
pass --no-push or set PUBLISH_NO_PUSH=1.
EOF
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

PRE_VERSION_REF=""
VERSION_TAG=""

cleanup_on_fail() {
  local exit_code=$?
  if [[ $exit_code -eq 0 ]]; then
    return
  fi

  cd "$ROOT_DIR"
  if [[ -n "${VERSION_TAG:-}" ]]; then
    echo "Cleaning up release tag ${VERSION_TAG} (script failed)." >&2
    git tag -d "$VERSION_TAG" >/dev/null 2>&1 || true
  fi
  if [[ -n "${PRE_VERSION_REF:-}" ]]; then
    echo "Reverting version bump commit (script failed)." >&2
    git reset --hard "$PRE_VERSION_REF" >/dev/null 2>&1 || true
  fi
}

trap cleanup_on_fail EXIT

main() {
  if [[ $# -lt 1 ]]; then
    echo "error: version bump argument missing" >&2
    usage
  fi

  local bump="$1"; shift || true
  local no_push="false"

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --no-push)
        no_push="true"
        ;;
      *)
        echo "error: unknown option '$1'" >&2
        usage
        ;;
    esac
    shift || true
  done

  if [[ ! $bump =~ ^(patch|minor|major)$ && ! $bump =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "error: invalid bump '$bump'" >&2
    usage
  fi

  ensure_clean_git

  cd "$ROOT_DIR"
  PRE_VERSION_REF="$(git rev-parse HEAD)"

  echo "› npm version $bump"
  npm version "$bump"

  # Resolve package identity post-bump
  local pkg_name
  local pkg_version
  pkg_name="$(node -p "require('./package.json').name")"
  pkg_version="$(node -p "require('./package.json').version")"
  VERSION_TAG="$(git describe --tags --exact-match HEAD 2>/dev/null || true)"
  echo "› preparing ${pkg_name}@${pkg_version}"

  echo "› npm install --package-lock-only"
  npm install --package-lock-only

  echo "› npm run build"
  npm run build

  echo "› npm test"
  npm test

  echo "› npm run smoke"
  npm run smoke

  if [[ "$no_push" == "true" || "${PUBLISH_NO_PUSH:-}" =~ ^([Yy][Ee][Ss]|[Yy]|1|true)$ ]]; then
    echo "› Skipping git push (no-push)."
    echo "  To publish upstream later, run: git push && git push --tags"
    return 0
  fi

  echo "› git push"
  git push
  echo "› git push --tags"
  git push --tags
}

ensure_clean_git() {
  cd "$ROOT_DIR"
  if ! git diff --quiet --ignore-submodules HEAD; then
    echo "error: git worktree has uncommitted changes" >&2
    exit 1
  fi
  if ! git diff --quiet --cached --ignore-submodules; then
    echo "error: git index has staged changes" >&2
    exit 1
  fi
}

main "$@"

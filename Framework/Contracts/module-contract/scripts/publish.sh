#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/publish.sh <patch|minor|major|x.y.z> [--no-push]

Examples:
  scripts/publish.sh patch
  scripts/publish.sh 0.2.0

The script requires a clean git worktree and an npm login to
https://registry.npmjs.org with publish access.

By default, the script pushes the version bump commit and tag. To skip pushing,
pass --no-push or set PUBLISH_NO_PUSH=1.
EOF
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

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
  ensure_npm_login

  cd "$ROOT_DIR"

  echo "› npm version $bump"
  npm version "$bump"

  echo "› npm run build"
  npm run build

  echo "› npm run test"
  npm run test

  echo "› npm publish --registry=https://registry.npmjs.org"
  npm publish --registry="https://registry.npmjs.org"

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

ensure_npm_login() {
  if ! npm whoami --registry="https://registry.npmjs.org" >/dev/null 2>&1; then
    cat >&2 <<'EOF'
error: not authenticated with https://registry.npmjs.org.
Run: npm login --registry=https://registry.npmjs.org --scope=@webstir-io
EOF
    exit 1
  fi
}

main "$@"

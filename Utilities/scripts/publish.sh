#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: Utilities/scripts/publish.sh <patch|minor|major|auto>

Examples:
  Utilities/scripts/publish.sh patch

The script requires a clean git worktree on main.
EOF
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/../.." && pwd)"

ensure_on_main() {
  local branch
  branch="$(git -C "$ROOT_DIR" rev-parse --abbrev-ref HEAD)"
  [[ "$branch" == "main" ]] || { echo "error: expected branch main, got $branch" >&2; exit 1; }
}

ensure_clean_git() {
  if [[ -n "$(git -C "$ROOT_DIR" status --porcelain --untracked-files=normal)" ]]; then
    echo "error: git worktree has uncommitted changes" >&2
    exit 1
  fi
}

main() {
  if [[ $# -lt 1 ]]; then
    echo "error: bump argument missing" >&2
    usage
  fi

  local bump="$1"
  case "$bump" in
    patch|minor|major|auto)
      ;;
    *)
      echo "error: invalid bump '$bump'" >&2
      usage
      ;;
  esac

  ensure_on_main
  ensure_clean_git

  echo "› gh workflow run release.yml --ref main -f bump=$bump"
  gh -R webstir-io/webstir-dotnet workflow run "release.yml" --ref "main" -f "bump=$bump"
}

main "$@"

#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: scripts/update-contract.sh [x.y.z|--latest] [--exact]

Updates @webstir-io/module-contract (defaults to latest when no version is provided),
installs deps, then builds and tests the backend package. Does NOT publish. If
everything passes, run scripts/publish.sh <bump> separately.

Examples:
  scripts/update-contract.sh                # use latest
  scripts/update-contract.sh --latest       # explicit latest
  scripts/update-contract.sh 0.1.8          # specific version (caret range)
  scripts/update-contract.sh 0.1.8 --exact  # set exact version instead of ^range
EOF
  exit 1
}

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}/.." && pwd)"

main() {
  local ver=""
  local exact="false"

  while [[ $# -gt 0 ]]; do
    case "$1" in
      --latest)
        ver="__resolve_latest__"
        ;;
      --exact)
        exact="true"
        ;;
      -h|--help)
        usage ;;
      *)
        # assume explicit version
        if [[ -n "$ver" && "$ver" != "__resolve_latest__" ]]; then
          echo "error: duplicate version argument '$1'" >&2
          usage
        fi
        ver="$1"
        ;;
    esac
    shift || true
  done

  # Default to latest if no version provided
  if [[ -z "$ver" || "$ver" == "__resolve_latest__" ]]; then
    echo "› Resolving latest @webstir-io/module-contract version"
    ver="$(npm view @webstir-io/module-contract version 2>/dev/null || true)"
    if [[ -z "$ver" ]]; then
      echo "error: unable to resolve latest @webstir-io/module-contract version" >&2
      exit 1
    fi
  fi

  if [[ ! $ver =~ ^[0-9]+\.[0-9]+\.[0-9]+$ ]]; then
    echo "error: invalid version '$ver' (expected x.y.z)" >&2
    usage
  fi

  cd "$ROOT_DIR"

  local spec
  if [[ "$exact" == "true" ]]; then
    spec="$ver"
  else
    spec="^$ver"
  fi

  echo "› Setting @webstir-io/module-contract to $spec"
  npm pkg set "dependencies.@webstir-io/module-contract=$spec"

  echo "› npm install (refresh lockfile)"
  npm install --no-audit --no-fund

  # Clarify versions in logs to avoid confusion with backend package version
  local backend_ver
  backend_ver="$(node -p "require('./package.json').version" 2>/dev/null || echo 'unknown')"
  local installed_contract
  installed_contract="$(npm ls @webstir-io/module-contract --json 2>/dev/null | node -e "let d='';process.stdin.on('data',c=>d+=c).on('end',()=>{try{const j=JSON.parse(d);const v=(j.dependencies&&j.dependencies['@webstir-io/module-contract']&&j.dependencies['@webstir-io/module-contract'].version)||'';console.log(v||'unknown')}catch{console.log('unknown')}})")"
  echo "› Backend package: @webstir-io/webstir-backend@${backend_ver}"
  echo "› Contract installed: @webstir-io/module-contract@${installed_contract}"

  echo "› npm run build"
  npm run build

  if npm run | grep -q "^  test"; then
    echo "› npm test"
    npm test
  fi

  if npm run | grep -q "^  smoke"; then
    echo "› npm run smoke"
    npm run smoke
  fi

  echo
  echo "Contract update complete: @webstir-io/module-contract@$spec"
}

main "$@"

#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: Utilities/scripts/sync-framework-versions.sh [options]

Sync framework package versions (catalog + templates) after a publish.

Options:
  -a, --all <ver|spec>        Set the same version/spec for backend, frontend, and testing
  -b, --backend <ver|spec>    Override @webstir-io/webstir-backend (e.g., 0.1.5 or @webstir-io/webstir-backend@0.1.5)
  -f, --frontend <ver|spec>   Override @webstir-io/webstir-frontend
  -t, --testing <ver|spec>    Override @webstir-io/webstir-testing
      --latest                Resolve any unspecified package(s) to the registry's latest tag
      --tag <name>            Dist-tag to resolve when using --latest (default: latest)
      --dry-run               Print what would run without executing
  -h, --help                  Show this help

Notes:
  - This updates Framework/Packaging/framework-packages.json and Engine/Resources/package.json
    by invoking the Framework 'packages sync' command with appropriate env overrides, then runs 'packages verify'.
  - Pass a bare version (e.g., 0.1.5) or a full registry spec (e.g., @webstir-io/webstir-backend@0.1.5).
EOF
}

here() { local s; s="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"; echo "$s"; }
root_dir() { local s; s="$(cd "$(here)/../.." && pwd)"; echo "$s"; }

make_spec() {
  # $1: package short name (backend|frontend|testing)
  # $2: user arg (version or full spec)
  local short="$1"; local val="$2";
  if [[ "$val" == *@* || "$val" == *@*:* || "$val" == *@*/* ]]; then
    echo "$val"
  else
    echo "@webstir-io/webstir-${short}@${val}"
  fi
}

resolve_latest() {
  # $1: package short name
  # $2: tag name (e.g., latest, next)
  local short="$1"; local tag="$2";
  local name="@webstir-io/webstir-${short}"
  local registry="${REGISTRY:-https://npm.pkg.github.com}"
  local ver
  # Try dist-tag lookup, then fall back to version (which is also latest)
  if ver=$(npm view "${name}" "dist-tags.${tag}" --registry="${registry}" 2>/dev/null); then
    :
  else
    ver=$(npm view "${name}" version --registry="${registry}" 2>/dev/null || true)
  fi
  echo -n "$ver"
}

DRY_RUN=0
USE_LATEST=0
LATEST_TAG="latest"
BACKEND_SPEC=""
FRONTEND_SPEC=""
TESTING_SPEC=""

if [[ $# -eq 0 ]]; then
  usage; exit 1
fi

while [[ $# -gt 0 ]]; do
  case "$1" in
    -a|--all)
      [[ $# -ge 2 ]] || { echo "error: --all requires a value" >&2; exit 1; }
      BACKEND_SPEC="$(make_spec backend "$2")"
      FRONTEND_SPEC="$(make_spec frontend "$2")"
      TESTING_SPEC="$(make_spec testing "$2")"
      shift 2;;
    -b|--backend)
      [[ $# -ge 2 ]] || { echo "error: --backend requires a value" >&2; exit 1; }
      BACKEND_SPEC="$(make_spec backend "$2")"
      shift 2;;
    -f|--frontend)
      [[ $# -ge 2 ]] || { echo "error: --frontend requires a value" >&2; exit 1; }
      FRONTEND_SPEC="$(make_spec frontend "$2")"
      shift 2;;
    -t|--testing)
      [[ $# -ge 2 ]] || { echo "error: --testing requires a value" >&2; exit 1; }
      TESTING_SPEC="$(make_spec testing "$2")"
      shift 2;;
    --dry-run)
      DRY_RUN=1; shift;;
    --latest)
      USE_LATEST=1; shift;;
    --tag)
      [[ $# -ge 2 ]] || { echo "error: --tag requires a value" >&2; exit 1; }
      LATEST_TAG="$2"; shift 2;;
    -h|--help)
      usage; exit 0;;
    *)
      echo "error: unknown arg '$1'" >&2; usage; exit 1;;
  esac
done

SYNC_FLAGS=()
[[ -n "$BACKEND_SPEC"  ]] && SYNC_FLAGS+=(--backend)
[[ -n "$FRONTEND_SPEC" ]] && SYNC_FLAGS+=(--frontend)
[[ -n "$TESTING_SPEC"  ]] && SYNC_FLAGS+=(--testing)

# Default to syncing all if no specific flags were provided
if [[ ${#SYNC_FLAGS[@]} -eq 0 ]]; then
  SYNC_FLAGS=(--backend --frontend --testing)
fi

# If requested, resolve latest for any package without an explicit override
if [[ $USE_LATEST -eq 1 ]]; then
  if [[ -z "$BACKEND_SPEC" ]]; then
    v=$(resolve_latest backend "$LATEST_TAG")
    [[ -n "$v" ]] && BACKEND_SPEC="@webstir-io/webstir-backend@${v}"
  fi
  if [[ -z "$FRONTEND_SPEC" ]]; then
    v=$(resolve_latest frontend "$LATEST_TAG")
    [[ -n "$v" ]] && FRONTEND_SPEC="@webstir-io/webstir-frontend@${v}"
  fi
  if [[ -z "$TESTING_SPEC" ]]; then
    v=$(resolve_latest testing "$LATEST_TAG")
    [[ -n "$v" ]] && TESTING_SPEC="@webstir-io/webstir-testing@${v}"
  fi
fi

ENV_EXPORT=(
  WEBSTIR_BACKEND_REGISTRY_SPEC="$BACKEND_SPEC"
  WEBSTIR_FRONTEND_REGISTRY_SPEC="$FRONTEND_SPEC"
  WEBSTIR_TEST_REGISTRY_SPEC="$TESTING_SPEC"
)

echo "› Syncing framework package versions"
echo "  backend : ${BACKEND_SPEC:-(no override)}"
echo "  frontend: ${FRONTEND_SPEC:-(no override)}"
echo "  testing : ${TESTING_SPEC:-(no override)}"
echo "  flags   : ${SYNC_FLAGS[*]}"

if [[ $DRY_RUN -eq 1 ]]; then
  echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages sync ${SYNC_FLAGS[*]}"
  echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages verify"
  exit 0
fi

pushd "$(root_dir)" >/dev/null
  # shellcheck disable=SC2086
  env ${ENV_EXPORT[@]} dotnet run --project Framework/Framework.csproj -- packages sync ${SYNC_FLAGS[*]}
  dotnet run --project Framework/Framework.csproj -- packages verify
popd >/dev/null

echo "Done."

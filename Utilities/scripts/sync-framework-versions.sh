#!/usr/bin/env bash

set -euo pipefail

usage() {
  cat <<'EOF'
Usage: Utilities/scripts/sync-framework-versions.sh [options]

Run with no options to resolve backend/frontend/testing to the registry's latest tag.

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
  local registry="${REGISTRY:-https://registry.npmjs.org}"
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

spec_version() {
  # Extract x.y.z from a spec like @scope/name@x.y.z or bare x.y.z
  local spec="$1"
  if [[ -z "$spec" ]]; then echo ""; return; fi
  if [[ "$spec" =~ ([0-9]+\.[0-9]+\.[0-9]+)$ ]]; then
    echo "${BASH_REMATCH[1]}"
  else
    echo ""
  fi
}

if [[ $# -eq 0 ]]; then
  USE_LATEST=1
else
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
fi

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

# Derive versions (if provided) for local Framework package bumps
BACKEND_VER="$(spec_version "$BACKEND_SPEC")"
FRONTEND_VER="$(spec_version "$FRONTEND_SPEC")"
TESTING_VER="$(spec_version "$TESTING_SPEC")"

# Determine local versions to avoid redundant bumps/builds
local_version() {
  local rel="$1"; node -e "console.log(require(require('path').resolve('${rel}')).version)" 2>/dev/null || true
}
BACKEND_LOCAL_VER="$(local_version Framework/Backend/package.json)"
FRONTEND_LOCAL_VER="$(local_version Framework/Frontend/package.json)"
TESTING_LOCAL_VER="$(local_version Framework/Testing/package.json)"

CHANGED_PACKAGES=()
if [[ -n "$BACKEND_VER" && "$BACKEND_VER" != "$BACKEND_LOCAL_VER" ]]; then CHANGED_PACKAGES+=(backend); fi
if [[ -n "$FRONTEND_VER" && "$FRONTEND_VER" != "$FRONTEND_LOCAL_VER" ]]; then CHANGED_PACKAGES+=(frontend); fi
if [[ -n "$TESTING_VER" && "$TESTING_VER" != "$TESTING_LOCAL_VER" ]]; then CHANGED_PACKAGES+=(testing); fi

if [[ $DRY_RUN -eq 1 ]]; then
  if [[ -n "$BACKEND_VER" && "$BACKEND_VER" != "$BACKEND_LOCAL_VER" ]]; then echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages bump --backend --set-version $BACKEND_VER"; fi
  if [[ -n "$FRONTEND_VER" && "$FRONTEND_VER" != "$FRONTEND_LOCAL_VER" ]]; then echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages bump --frontend --set-version $FRONTEND_VER"; fi
  if [[ -n "$TESTING_VER" && "$TESTING_VER" != "$TESTING_LOCAL_VER" ]]; then echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages bump --testing --set-version $TESTING_VER"; fi
  # Only sync changed packages; verify still runs for all
  if [[ ${#CHANGED_PACKAGES[@]} -gt 0 ]]; then
    pkgs=( ); for p in "${CHANGED_PACKAGES[@]}"; do pkgs+=(--package "$p"); done
    echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages sync ${pkgs[*]}"
  else
    echo "(dry-run) packages up-to-date; skipping sync"
  fi
  echo "(dry-run) dotnet run --project Framework/Framework.csproj -- packages verify --all"
  exit 0
fi

pushd "$(root_dir)" >/dev/null
  # Bump local Framework package versions to match resolved specs (if provided)
  if [[ -n "$BACKEND_VER" ]]; then
    dotnet run --project Framework/Framework.csproj -- packages bump --backend --set-version "$BACKEND_VER"
  fi
  if [[ -n "$FRONTEND_VER" ]]; then
    dotnet run --project Framework/Framework.csproj -- packages bump --frontend --set-version "$FRONTEND_VER"
  fi
  if [[ -n "$TESTING_VER" ]]; then
    dotnet run --project Framework/Framework.csproj -- packages bump --testing --set-version "$TESTING_VER"
  fi
  # Sync only changed packages to avoid needless builds
  if [[ ${#CHANGED_PACKAGES[@]} -gt 0 ]]; then
    pkgs=( ); for p in "${CHANGED_PACKAGES[@]}"; do pkgs+=(--package "$p"); done
    # shellcheck disable=SC2086
    env ${ENV_EXPORT[@]} dotnet run --project Framework/Framework.csproj -- packages sync ${pkgs[*]}
  else
    echo "[sync] Packages already at target versions; skipping sync build."
  fi
  dotnet run --project Framework/Framework.csproj -- packages verify --all
popd >/dev/null

echo "Done."

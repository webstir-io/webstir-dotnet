#!/usr/bin/env bash
set -euo pipefail

# Helper to bump framework package versions and publish them via the console.
# Options:
#   --bump <patch|minor|major>   Defaults to patch.
#   --dry-run                    Show the next version without publishing.
# Additional arguments are forwarded to `framework packages publish`.

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "${SCRIPT_DIR}/../.." && pwd)"
FRAMEWORK_PROJ="${REPO_ROOT}/framework/Framework.csproj"
BUMP_SCRIPT="${SCRIPT_DIR}/bump-version.mjs"

if [[ ! -f "${FRAMEWORK_PROJ}" ]]; then
  echo "publish: unable to locate framework project at ${FRAMEWORK_PROJ}" >&2
  exit 1
fi

if [[ ! -f "${BUMP_SCRIPT}" ]]; then
  echo "publish: missing bump script at ${BUMP_SCRIPT}" >&2
  exit 1
fi

BUMP="patch"
DRY_RUN="false"
PASSTHRU=()

while [[ $# -gt 0 ]]; do
  case "$1" in
    --bump|-b)
      BUMP="${2:-}"
      if [[ -z "$BUMP" ]]; then
        echo "publish: --bump requires a value" >&2
        exit 1
      fi
      shift 2
      ;;
    --dry-run)
      DRY_RUN="true"
      shift
      ;;
    --)
      shift
      PASSTHRU+=("$@")
      break
      ;;
    *)
      PASSTHRU+=("$1")
      shift
      ;;
  esac
done

if ! [[ "$BUMP" =~ ^(patch|minor|major)$ ]]; then
  echo "publish: unsupported bump type '$BUMP'" >&2
  exit 1
fi

cd "${REPO_ROOT}"

if [[ "$DRY_RUN" == "true" ]]; then
  echo "publish: dry run — bumping only"
  node "$BUMP_SCRIPT" --bump "$BUMP" --dry-run
  exit 0
fi

DEFAULT_NPMRC="$REPO_ROOT/.npmrc"
if [[ -n "${NPM_CONFIG_USERCONFIG:-}" ]]; then
  if [[ ! -f "$NPM_CONFIG_USERCONFIG" && -f "$DEFAULT_NPMRC" ]]; then
    echo "publish: NPM_CONFIG_USERCONFIG points to '$NPM_CONFIG_USERCONFIG' but it does not exist; using $DEFAULT_NPMRC instead." >&2
    export NPM_CONFIG_USERCONFIG="$DEFAULT_NPMRC"
  fi
elif [[ -f "$DEFAULT_NPMRC" ]]; then
  export NPM_CONFIG_USERCONFIG="$DEFAULT_NPMRC"
fi

if [[ -z "${GH_PACKAGES_TOKEN:-}" ]]; then
  echo "publish: GH_PACKAGES_TOKEN is not set; npm auth will fail." >&2
  exit 1
fi

NEW_VERSION=$(node "$BUMP_SCRIPT" --bump "$BUMP")

if [[ -z "$NEW_VERSION" ]]; then
  echo "publish: failed to compute new version" >&2
  exit 1
fi

echo "publish: bumped packages to $NEW_VERSION"

CMD=(dotnet run --project "$FRAMEWORK_PROJ" -- packages publish)
if (( ${#PASSTHRU[@]} )); then
  CMD+=("${PASSTHRU[@]}")
fi

"${CMD[@]}"

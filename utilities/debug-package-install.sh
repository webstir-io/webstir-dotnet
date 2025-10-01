#!/usr/bin/env bash
set -euo pipefail

# Debug helper: install the framework packages in a temp workspace and report the result.
# This mirrors the CLI deploy script's npm usage without involving dotnet or the workflow layer.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
WORK_DIR_DEFAULT="${ROOT_DIR}/utilities/debug-package-install-workspace"
WORK_DIR="${WORKSPACE_DIR:-$WORK_DIR_DEFAULT}"
KEEP_TMP=${KEEP_TMP:-0}

if [[ "$KEEP_TMP" != "1" ]]; then
  rm -rf "$WORK_DIR"
fi

mkdir -p "$WORK_DIR"

printf 'repo:        %s\n' "$ROOT_DIR"
printf 'workspace:   %s\n' "$WORK_DIR"

DEFAULT_NPMRC="${ROOT_DIR}/.npmrc"
if [[ -n "${NPM_CONFIG_USERCONFIG:-}" ]]; then
  if [[ ! -f "$NPM_CONFIG_USERCONFIG" ]]; then
    if [[ -f "$DEFAULT_NPMRC" ]]; then
      printf 'userconfig:  %s (missing, falling back)\n' "$NPM_CONFIG_USERCONFIG"
      export NPM_CONFIG_USERCONFIG="$DEFAULT_NPMRC"
    else
      printf 'userconfig:  %s (missing, no fallback)\n' "$NPM_CONFIG_USERCONFIG"
    fi
  else
    printf 'userconfig:  %s\n' "$NPM_CONFIG_USERCONFIG"
  fi
else
  if [[ -f "$DEFAULT_NPMRC" ]]; then
    export NPM_CONFIG_USERCONFIG="$DEFAULT_NPMRC"
    printf 'userconfig:  %s (set)\n' "$NPM_CONFIG_USERCONFIG"
  else
    printf 'userconfig:  <unset> (no .npmrc found)\n'
  fi
fi

if [[ -n "${GH_PACKAGES_TOKEN+x}" ]]; then
  printf 'GH token len:%d\n' "${#GH_PACKAGES_TOKEN}"
else
  printf 'GH token len:0\n'
fi

printf '\n== npm whoami (GitHub Packages) ==\n'
npm whoami --registry=https://npm.pkg.github.com

cat >"${WORK_DIR}/package.json" <<'JSON'
{
  "name": "webstir-debug",
  "version": "1.0.0",
  "private": true,
  "dependencies": {
    "@electric-coding-llc/webstir-frontend": "@electric-coding-llc/webstir-frontend@0.3.4",
    "@electric-coding-llc/webstir-test": "@electric-coding-llc/webstir-test@0.3.4"
  }
}
JSON

printf '\n== npm install in fresh workspace ==\n'
(
  cd "${WORK_DIR}" && npm install --loglevel=info
)

printf '\n== Inspect installed packages ==\n'
for pkg in webstir-frontend webstir-test; do
  pkg_dir="${WORK_DIR}/node_modules/@electric-coding-llc/${pkg}"
  if [[ -f "${pkg_dir}/package.json" ]]; then
    version=$(node -pe "require(require('path').resolve(process.argv[1])).version" "${pkg_dir}/package.json" 2>/dev/null || echo 'unknown')
    printf -- '-- %s ok (%s)\n' "$pkg" "$version"
  else
    printf -- '-- %s missing\n' "$pkg"
    ls -l "${WORK_DIR}/node_modules/@electric-coding-llc" || true
  fi
done

printf '\n== Done ==\n'

# Show most recent npm log for convenience.
if command -v ls >/dev/null 2>&1; then
  latest_log=$(ls -t "$HOME/.npm/_logs" 2>/dev/null | head -n1 || true)
  if [[ -n "$latest_log" ]]; then
    printf '\n== Recent npm log (%s) ==\n' "$latest_log"
    tail -n 40 "$HOME/.npm/_logs/${latest_log}"
  fi
fi

if [[ "$KEEP_TMP" = "1" ]]; then
  printf '\nWorkspace retained at %s (KEEP_TMP=1).\n' "$WORK_DIR"
fi

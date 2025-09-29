#!/bin/sh
set -euo pipefail

REGISTRY_URL="${NPM_REGISTRY:-http://registry:4873}"
FRONTEND_PACKAGE="${FRONTEND_PACKAGE:-@webstir/frontend}"
FRONTEND_DIR="${FRONTEND_DIR:-framework/frontend}"
TEST_PACKAGE="${TEST_PACKAGE:-@webstir/test}"
TEST_DIR="${TEST_DIR:-framework/testing}"
WORKSPACE="${WORKSPACE:-/workspace}"
USER_NPMRC="${NPM_USERCONFIG:-/npmrc/.npmrc}"
FALLBACK_NPMRC="${NPMRC_FALLBACK:-}"

if [ ! -f "${USER_NPMRC}" ] && [ -n "${FALLBACK_NPMRC}" ] && [ -f "${FALLBACK_NPMRC}" ]; then
  cp "${FALLBACK_NPMRC}" "${USER_NPMRC}"
fi

if [ ! -f "${USER_NPMRC}" ]; then
  echo "publisher: missing ${USER_NPMRC}, skipping publish." >&2
  exit 0
fi

export NPM_CONFIG_USERCONFIG="${USER_NPMRC}"

publish_if_missing() {
  package=$1
  directory=$2

  version=$(node -p "require('./${directory}/package.json').version")
  spec="${package}@${version}"

  if npm view "${spec}" version --registry "${REGISTRY_URL}" >/dev/null 2>&1; then
    echo "publisher: ${spec} already present in ${REGISTRY_URL}"
  else
    echo "publisher: publishing ${spec} to ${REGISTRY_URL}"
    temp_dir=$(mktemp -d)
    tar cf - -C "${directory}" . | tar xf - -C "${temp_dir}"
    (
      cd "${temp_dir}"
      npm ci --silent >/dev/null 2>&1
      if npm run build --silent >/dev/null 2>&1; then
        echo "publisher: built ${spec} before publish"
      fi
      npm publish --registry "${REGISTRY_URL}" --access=public >/dev/null 2>&1
    )
    rm -rf "${temp_dir}"
    echo "publisher: published ${spec}"
  fi
}

cd "${WORKSPACE}"

publish_if_missing "${FRONTEND_PACKAGE}" "${FRONTEND_DIR}"
publish_if_missing "${TEST_PACKAGE}" "${TEST_DIR}"

echo "publisher: done"

#!/bin/sh
set -euo pipefail

OUT_PATH="${OUT:-/tmp/npmrc}"
NPM_USERNAME="${NPM_USERNAME:-webstir}"
NPM_PASSWORD="${NPM_PASSWORD:-webstir}"
NPM_EMAIL="${NPM_EMAIL:-dev@local.test}"
NPM_REGISTRY="${NPM_REGISTRY:-http://registry:4873}"
NPM_SCOPE="${NPM_SCOPE:-@electric-coding-llc}"

mkdir -p "${OUT_PATH}"
CONFIG_PATH="${OUT_PATH}/.npmrc"

# Install the helper quietly
npm install -g npm-cli-login >/dev/null 2>&1

npm-cli-login \
  -u "${NPM_USERNAME}" \
  -p "${NPM_PASSWORD}" \
  -e "${NPM_EMAIL}" \
  -r "${NPM_REGISTRY}" \
  -s "${NPM_SCOPE}" \
  --config-path "${CONFIG_PATH}"

echo "Generated .npmrc at ${CONFIG_PATH} for registry ${NPM_REGISTRY}"

HOST_DIR="${HOST_NPMRC_DIR:-}"
if [ -n "${HOST_DIR}" ] && [ -d "${HOST_DIR}" ]; then
  cp "${CONFIG_PATH}" "${HOST_DIR}/.npmrc"
  echo "Copied .npmrc to ${HOST_DIR}/.npmrc"
fi

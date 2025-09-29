#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="${ROOT_DIR}/framework/testing"
TOOLS_DIR="${ROOT_DIR}/Engine/Resources/tools"
FRAMEWORK_OUT_DIR="${ROOT_DIR}/framework/out"
MANIFEST_PATH="${TOOLS_DIR}/testing-package.json"
LOCAL_MANIFEST_PATH="${FRAMEWORK_OUT_DIR}/manifest.json"
TEST_REGISTRY_SPEC="${WEBSTIR_TEST_REGISTRY_SPEC:-}"

cd "${PACKAGE_DIR}"

echo "Installing dependencies..."
npm ci --silent

echo "Building TypeScript sources..."
npm run build --silent

# Remove any previous tarballs generated locally before creating a new one.
rm -f webstir-test-*.tgz

echo "Packing @webstir/test..."
TARBALL_NAME="$(npm pack --silent)"

VERSION="$(node -e "const fs=require('fs');const data=JSON.parse(fs.readFileSync('package.json','utf8'));process.stdout.write(data.version);")"
SAFE_VERSION="${VERSION//./-}"
TARGET_TARBALL="webstir-test-${SAFE_VERSION}.tgz"

REPO_PACKAGE_DIR="${FRAMEWORK_OUT_DIR}/testing/${VERSION}"
REPO_TARBALL_PATH="${REPO_PACKAGE_DIR}/${TARGET_TARBALL}"

if [[ "${TARBALL_NAME}" != "${TARGET_TARBALL}" ]]; then
  mv "${TARBALL_NAME}" "${TARGET_TARBALL}"
fi

mkdir -p "${REPO_PACKAGE_DIR}"
rm -f "${REPO_PACKAGE_DIR}/"webstir-test-*.tgz
cp "${TARGET_TARBALL}" "${REPO_TARBALL_PATH}"

mkdir -p "${TOOLS_DIR}"
rm -f "${TOOLS_DIR}/"webstir-test-*.tgz
cp "${TARGET_TARBALL}" "${TOOLS_DIR}/${TARGET_TARBALL}"

HASH="$(node -e "const fs=require('fs');const crypto=require('crypto');const file=process.argv[1];const hash=crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');process.stdout.write(hash);" "${REPO_TARBALL_PATH}")"

node "${ROOT_DIR}/utilities/update-package-manifest.js" "${LOCAL_MANIFEST_PATH}" "@webstir/test" "${VERSION}" "${REPO_TARBALL_PATH}" "${HASH}" "${TEST_REGISTRY_SPEC}"

cat <<JSON > "${MANIFEST_PATH}"
{
  "name": "@webstir/test",
  "version": "${VERSION}",
  "fileName": "${TARGET_TARBALL}",
  "dependency": "file:./.tools/${TARGET_TARBALL}",
  "hash": "${HASH}"
}
JSON

echo "Updating Engine/Resources/package.json dependency..."
node -e "
  const fs = require('fs');
  const filePath = process.argv[1];
  const tarball = process.argv[2];
  const data = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  data.dependencies = data.dependencies ?? {};
  data.dependencies['@webstir/test'] = 'file:./.tools/' + tarball;
  fs.writeFileSync(filePath, JSON.stringify(data, null, 2) + '\n');
" "${ROOT_DIR}/Engine/Resources/package.json" "${TARGET_TARBALL}"

# Clean build artifacts to avoid polluting the repo.
rm -rf "${PACKAGE_DIR}/node_modules" "${PACKAGE_DIR}/dist" "${PACKAGE_DIR}/webstir-test-"*.tgz

echo "Done."

#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="${ROOT_DIR}/framework/testing"
TOOLS_DIR="${ROOT_DIR}/Engine/Resources/tools"
MANIFEST_PATH="${TOOLS_DIR}/testing-package.json"

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

if [[ "${TARBALL_NAME}" != "${TARGET_TARBALL}" ]]; then
  mv "${TARBALL_NAME}" "${TARGET_TARBALL}"
fi

mkdir -p "${TOOLS_DIR}"
rm -f "${TOOLS_DIR}/"webstir-test-*.tgz
cp "${TARGET_TARBALL}" "${TOOLS_DIR}/${TARGET_TARBALL}"

cat <<JSON > "${MANIFEST_PATH}"
{
  "name": "@webstir/test",
  "version": "${VERSION}",
  "fileName": "${TARGET_TARBALL}",
  "dependency": "file:./.tools/${TARGET_TARBALL}"
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

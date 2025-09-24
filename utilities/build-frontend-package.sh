#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
PACKAGE_DIR="${ROOT_DIR}/framework/frontend"
TOOLS_DIR="${ROOT_DIR}/Engine/Resources/tools"
FRAMEWORK_OUT_DIR="${ROOT_DIR}/framework/out"
MANIFEST_PATH="${TOOLS_DIR}/frontend-package.json"
LOCAL_MANIFEST_PATH="${FRAMEWORK_OUT_DIR}/manifest.json"

cd "${PACKAGE_DIR}"

echo "Installing frontend dependencies..."
npm ci --silent

echo "Building @webstir/frontend..."
npm run build --silent

rm -f webstir-frontend-*.tgz

echo "Packing @webstir/frontend..."
TARBALL_NAME="$(npm pack --silent)"

VERSION="$(node -p "require('./package.json').version")"
SAFE_VERSION="${VERSION//./-}"
TARGET_TARBALL="webstir-frontend-${SAFE_VERSION}.tgz"

REPO_PACKAGE_DIR="${FRAMEWORK_OUT_DIR}/frontend/${VERSION}"
REPO_TARBALL_PATH="${REPO_PACKAGE_DIR}/${TARGET_TARBALL}"

if [[ "${TARBALL_NAME}" != "${TARGET_TARBALL}" ]]; then
  mv "${TARBALL_NAME}" "${TARGET_TARBALL}"
fi

mkdir -p "${REPO_PACKAGE_DIR}"
rm -f "${REPO_PACKAGE_DIR}/"webstir-frontend-*.tgz
cp "${TARGET_TARBALL}" "${REPO_TARBALL_PATH}"

mkdir -p "${TOOLS_DIR}"
rm -f "${TOOLS_DIR}/"webstir-frontend-*.tgz
cp "${TARGET_TARBALL}" "${TOOLS_DIR}/${TARGET_TARBALL}"

HASH="$(node -e "const fs=require('fs');const crypto=require('crypto');const file=process.argv[1];const hash=crypto.createHash('sha256').update(fs.readFileSync(file)).digest('hex');process.stdout.write(hash);" "${REPO_TARBALL_PATH}")"

node "${ROOT_DIR}/utilities/update-package-manifest.js" "${LOCAL_MANIFEST_PATH}" "@webstir/frontend" "${VERSION}" "${REPO_TARBALL_PATH}" "${HASH}"

cat <<JSON > "${MANIFEST_PATH}"
{
  "name": "@webstir/frontend",
  "version": "${VERSION}",
  "fileName": "${TARGET_TARBALL}",
  "dependency": "file:./.tools/${TARGET_TARBALL}",
  "hash": "${HASH}"
}
JSON

echo "Updating Engine/Resources/package.json dependency..."
node -e "
  const fs = require('fs');
  const pkgPath = process.argv[1];
  const tarball = process.argv[2];
  const data = JSON.parse(fs.readFileSync(pkgPath, 'utf8'));
  data.dependencies = data.dependencies ?? {};
  data.dependencies['@webstir/frontend'] = 'file:./.tools/' + tarball;
  fs.writeFileSync(pkgPath, JSON.stringify(data, null, 2) + '\n');
" "${ROOT_DIR}/Engine/Resources/package.json" "${TARGET_TARBALL}"

echo "Done."

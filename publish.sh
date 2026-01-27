#!/usr/bin/env bash

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ROOT_DIR="$(cd "${SCRIPT_DIR}" && pwd)"
PROJECT_PATH="${ROOT_DIR}/CLI/CLI.csproj"

INSTALL_DIR="${WEBSTIR_INSTALL_DIR:-${HOME}/.local/bin}"

detect_rid() {
  if [[ -n "${WEBSTIR_RID:-}" ]]; then
    echo "${WEBSTIR_RID}"
    return
  fi

  local rid
  rid="$(dotnet --info | awk -F': ' '/^RID:/{print $2; exit}')"
  if [[ -n "${rid}" ]]; then
    echo "${rid}"
    return
  fi

  local os arch
  case "$(uname -s)" in
    Darwin) os="osx" ;;
    Linux) os="linux" ;;
    *)
      echo "error: unsupported OS: $(uname -s)" >&2
      exit 1
      ;;
  esac

  case "$(uname -m)" in
    arm64|aarch64) arch="arm64" ;;
    x86_64|amd64) arch="x64" ;;
    *)
      echo "error: unsupported architecture: $(uname -m)" >&2
      exit 1
      ;;
  esac

  echo "${os}-${arch}"
}

RID="$(detect_rid)"
PUBLISH_DIR="${ROOT_DIR}/.artifacts/publish/${RID}"
TARGET="${PUBLISH_DIR}/webstir"
LINK_PATH="${INSTALL_DIR}/webstir"
CLI_BINARY="${PUBLISH_DIR}/CLI"

echo "› Publishing ${PROJECT_PATH} for RID ${RID}"
dotnet publish "${PROJECT_PATH}" \
  -c Release \
  -r "${RID}" \
  --self-contained true \
  -p:PublishSingleFile=true \
  -o "${PUBLISH_DIR}"

if [[ -f "${CLI_BINARY}" ]]; then
  mv -f "${CLI_BINARY}" "${TARGET}"
fi

if [[ ! -f "${TARGET}" ]]; then
  echo "error: expected published binary at ${TARGET}" >&2
  echo "publish directory contents:" >&2
  ls -la "${PUBLISH_DIR}" >&2
  exit 1
fi

mkdir -p "${INSTALL_DIR}"
ln -sf "${TARGET}" "${LINK_PATH}"

echo "✓ Installed: ${LINK_PATH} -> ${TARGET}"
if ! command -v webstir >/dev/null 2>&1; then
  echo "note: ${INSTALL_DIR} is not on PATH in this shell." >&2
  echo "add this to your shell profile (~/.zshrc):" >&2
  echo "  export PATH=\"${INSTALL_DIR}:\$PATH\"" >&2
fi

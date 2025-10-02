#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE_NAME="webstir-local-ci:latest"

echo "[docker-ci] Building container image (${IMAGE_NAME})..."
docker build -f "$ROOT_DIR/utilities/docker/ci.Dockerfile" -t "$IMAGE_NAME" "$ROOT_DIR"

echo "[docker-ci] Running local CI inside container..."
docker run --rm \
  -e GH_PACKAGES_TOKEN="${GH_PACKAGES_TOKEN:-}" \
  -v "$ROOT_DIR":/workspace \
  -w /workspace \
  "$IMAGE_NAME"


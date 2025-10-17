#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
IMAGE_NAME="webstir-local-ci:latest"
PARENT_DIR="$(dirname "$ROOT_DIR")"
ROOT_NAME="$(basename "$ROOT_DIR")"

echo "[docker-ci] Building container image (${IMAGE_NAME})..."
docker build -f "$ROOT_DIR/utilities/docker/ci.Dockerfile" -t "$IMAGE_NAME" "$ROOT_DIR"

echo "[docker-ci] Running local CI inside container..."
docker run --rm \
  -e GH_PACKAGES_TOKEN="${GH_PACKAGES_TOKEN:-}" \
  -e NODE_AUTH_TOKEN="${GH_PACKAGES_TOKEN:-}" \
  -v "$ROOT_DIR/.npmrc":/root/.npmrc:ro \
  -v "$PARENT_DIR":/workspaces \
  -w "/workspaces/$ROOT_NAME" \
  "$IMAGE_NAME"

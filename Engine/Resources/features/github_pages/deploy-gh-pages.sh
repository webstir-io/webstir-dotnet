#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist/frontend"
PUBLISH_CMD="${WEBSTIR_PUBLISH_CMD:-webstir publish --frontend-mode ssg}"
REMOTE="${GH_PAGES_REMOTE:-origin}"
BRANCH="${GH_PAGES_BRANCH:-gh-pages}"
COMMIT_MESSAGE="${GH_PAGES_COMMIT_MESSAGE:-Deploy}"

WORKTREE_DIR=""
cleanup() {
  if [[ -n "${WORKTREE_DIR}" && -d "${WORKTREE_DIR}" ]]; then
    git worktree remove --force "$WORKTREE_DIR" >/dev/null 2>&1 || true
    rm -rf "$WORKTREE_DIR"
  fi
}
trap cleanup EXIT

echo "[gh-pages] Publishing static site..."
eval "$PUBLISH_CMD"

if [[ ! -d "$DIST_DIR" ]]; then
  echo "[gh-pages] Expected dist at $DIST_DIR but it was not found." >&2
  echo "[gh-pages] Run: webstir publish --frontend-mode ssg" >&2
  exit 1
fi

git fetch "$REMOTE" "$BRANCH" >/dev/null 2>&1 || true

WORKTREE_DIR="$(mktemp -d 2>/dev/null || mktemp -d -t webstir-gh-pages)"
if git show-ref --verify --quiet "refs/remotes/$REMOTE/$BRANCH"; then
  git worktree add "$WORKTREE_DIR" "$REMOTE/$BRANCH" >/dev/null
else
  git worktree add -b "$BRANCH" "$WORKTREE_DIR" >/dev/null
fi

rm -rf "$WORKTREE_DIR"/*
cp -R "$DIST_DIR"/. "$WORKTREE_DIR"/
touch "$WORKTREE_DIR/.nojekyll"

git -C "$WORKTREE_DIR" add -A
if git -C "$WORKTREE_DIR" diff --cached --quiet; then
  echo "[gh-pages] No changes to deploy."
  exit 0
fi

git -C "$WORKTREE_DIR" commit -m "$COMMIT_MESSAGE"
if [[ -n "${GH_PAGES_NO_PUSH:-}" ]]; then
  echo "[gh-pages] Skipping push (GH_PAGES_NO_PUSH is set)."
  exit 0
fi

git -C "$WORKTREE_DIR" push "$REMOTE" HEAD:"$BRANCH"
echo "[gh-pages] Deployed to $REMOTE/$BRANCH"

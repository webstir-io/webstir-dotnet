#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
DIST_DIR="$ROOT_DIR/dist/frontend"
REMOTE="${GH_PAGES_REMOTE:-origin}"
BRANCH="${GH_PAGES_BRANCH:-gh-pages}"
COMMIT_MESSAGE="${GH_PAGES_COMMIT_MESSAGE:-Deploy}"
COMMIT_NAME="${GH_PAGES_COMMIT_NAME:-github-actions[bot]}"
COMMIT_EMAIL="${GH_PAGES_COMMIT_EMAIL:-github-actions[bot]@users.noreply.github.com}"

WORKTREE_DIR=""
cleanup() {
  if [[ -n "${WORKTREE_DIR}" && -d "${WORKTREE_DIR}" ]]; then
    git worktree remove --force "$WORKTREE_DIR" >/dev/null 2>&1 || true
    rm -rf "$WORKTREE_DIR"
  fi
}
trap cleanup EXIT

publish_site() {
  if [[ -n "${WEBSTIR_PUBLISH_CMD:-}" ]]; then
    echo "[gh-pages] Running WEBSTIR_PUBLISH_CMD..."
    bash -lc "${WEBSTIR_PUBLISH_CMD}"
    return
  fi

  local frontend_cli="$ROOT_DIR/node_modules/.bin/webstir-frontend"
  if [[ ! -f "$frontend_cli" ]]; then
    echo "[gh-pages] Missing $frontend_cli" >&2
    echo "[gh-pages] Install dependencies first (npm install / pnpm install)." >&2
    exit 1
  fi

  "$frontend_cli" publish -w "$ROOT_DIR" -m ssg
}

echo "[gh-pages] Publishing static site..."
publish_site

if [[ ! -d "$DIST_DIR" ]]; then
  echo "[gh-pages] Expected dist at $DIST_DIR but it was not found." >&2
  echo "[gh-pages] Run: $ROOT_DIR/node_modules/.bin/webstir-frontend publish -w $ROOT_DIR -m ssg" >&2
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
for entry in "$WORKTREE_DIR"/.*; do
  name="$(basename "$entry")"
  if [[ "$name" == "." || "$name" == ".." || "$name" == ".git" ]]; then
    continue
  fi
  rm -rf "$entry"
done
cp -R "$DIST_DIR"/. "$WORKTREE_DIR"/
touch "$WORKTREE_DIR/.nojekyll"

if [[ -z "$(git -C "$WORKTREE_DIR" config user.name || true)" ]]; then
  git -C "$WORKTREE_DIR" config user.name "$COMMIT_NAME"
fi

if [[ -z "$(git -C "$WORKTREE_DIR" config user.email || true)" ]]; then
  git -C "$WORKTREE_DIR" config user.email "$COMMIT_EMAIL"
fi

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

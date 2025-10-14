#!/usr/bin/env bash
set -euo pipefail

# Combined whitespace fixer:
# 1) Normalize all .cs files to LF, trim trailing spaces, ensure final newline.
# 2) Temporarily disable charset in .editorconfig and run `dotnet format whitespace`
#    to apply Roslyn whitespace rules (avoids charset crash), then restore.

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

echo "[1/2] Normalizing .cs files (LF, trim trailing, final newline) ..."
python3 - << 'PY'
import pathlib, re, sys

def fix_file(path: pathlib.Path):
    try:
        data = path.read_bytes()
    except Exception as e:
        print(f"skip {path}: {e}", file=sys.stderr)
        return False

    # Normalize newlines to LF
    data = data.replace(b"\r\n", b"\n").replace(b"\r", b"\n")

    try:
        text = data.decode('utf-8')
    except UnicodeDecodeError:
        text = data.decode('utf-8', errors='replace')

    # Strip trailing whitespace on each line
    lines = text.split('\n')
    lines = [re.sub(r"[ \t]+$", "", line) for line in lines]

    # Ensure final newline
    if lines and lines[-1] != '':
        text = '\n'.join(lines) + '\n'
    else:
        text = '\n'.join(lines)

    new_data = text.encode('utf-8')
    if new_data != data:
        path.write_bytes(new_data)
        return True
    return False

changed = 0
for p in pathlib.Path('.').rglob('*.cs'):
    if fix_file(p):
        changed += 1
print(f"normalized {changed} files")
PY

echo "[2/2] Running dotnet format whitespace with charset workaround ..."
EC=".editorconfig"
BAK=".editorconfig.bak"
TMP=".editorconfig.tmp"

if [[ -f "$EC" ]]; then
  sed -E 's/^([[:space:]]*charset[[:space:]]*=)/# \1/g' "$EC" > "$TMP"
  mv -f "$EC" "$BAK"
  mv -f "$TMP" "$EC"
  trap 'mv -f "$BAK" "$EC" 2>/dev/null || true' EXIT
fi

# Apply whitespace fixes; ignore non-fatal exit while logging failures
format_failed=false

projects_output="$(dotnet sln list 2>/dev/null || true)"
if [[ "$projects_output" == *"Project(s)"* ]]; then
  echo "Formatting whitespace for individual projects..."
  sln_projects=()
  while IFS= read -r project_path; do
    sln_projects+=("$project_path")
  done < <(printf '%s\n' "$projects_output" | tail -n +3)

  for project in "${sln_projects[@]}"; do
    project="$(echo "$project" | xargs)"
    if [[ -z "$project" ]]; then
      continue
    fi

    echo "  dotnet format whitespace --no-restore ${project}"
    if ! dotnet format whitespace "$project" --no-restore; then
      format_failed=true
    fi
  done
else
  echo "Formatting whitespace for detected workspace..."
  if ! dotnet format whitespace --no-restore; then
    format_failed=true
  fi
fi

if [[ "$format_failed" == "true" ]]; then
  echo "dotnet format whitespace encountered errors." >&2
fi

if [[ -f "$BAK" ]]; then
  mv -f "$BAK" "$EC"
  trap - EXIT
fi

echo "Whitespace fixes complete."

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

# Apply whitespace fixes; ignore non-fatal exit
dotnet format whitespace --no-restore || true

if [[ -f "$BAK" ]]; then
  mv -f "$BAK" "$EC"
  trap - EXIT
fi

echo "Whitespace fixes complete."

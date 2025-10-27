#!/usr/bin/env bash
set -euo pipefail

# Format and then build the solution from repo root.
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

run_dotnet_format_scope() {
    local mode="$1"
    local description="$2"
    local format_failed=false

    echo "Running dotnet format ${description} across solution..."
    if ! dotnet format "$mode" --no-restore Webstir.sln; then
        format_failed=true
    fi

    if [[ "$format_failed" == "true" ]]; then
        echo "dotnet format ${mode} encountered errors." >&2
    fi
}

echo "Fixing whitespace..."
normalize_cs_file() {
    local file="$1"
    local tmp
    tmp="$(mktemp)" || return 2

    awk '{
            sub(/\r$/, "");
            sub(/[ \t]+$/, "");
            print $0;
        }' "$file" > "$tmp"

    if ! cmp -s "$file" "$tmp"; then
        mv "$tmp" "$file"
        return 0
    fi

    rm -f "$tmp"
    return 1
}

normalized_count=0
while IFS= read -r -d '' cs_file; do
    if [[ ! -f "$cs_file" ]]; then
        continue
    fi

    if normalize_cs_file "$cs_file"; then
        normalized_count=$((normalized_count + 1))
    else
        result=$?
        if [[ "$result" -gt 1 ]]; then
            echo "Failed to normalize $cs_file" >&2
            exit "$result"
        fi
    fi
done < <(find . -type f -name '*.cs' -print0)

echo "normalized ${normalized_count} files"

run_dotnet_format_scope "whitespace" "(whitespace)"
run_dotnet_format_scope "style" "(style)"
run_dotnet_format_scope "analyzers" "(analyzers)"

echo "Validating contract schemas..."
if ! npm run validate:contracts --silent; then
    echo "Contract schema validation failed." >&2
    exit 1
fi

echo "Building solution..."
dotnet build Webstir.sln -v minimal

echo "Done."

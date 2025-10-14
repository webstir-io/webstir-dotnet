#!/usr/bin/env bash
set -euo pipefail

# Format and then build the solution from repo root.
ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

run_dotnet_format_scope() {
    local mode="$1"
    local description="$2"
    local format_failed=false

    local projects_output
    projects_output="$(dotnet sln list 2>/dev/null || true)"

    if [[ "$projects_output" == *"Project(s)"* ]]; then
        echo "Running dotnet format ${description} across individual projects..."
        local sln_projects=()
        while IFS= read -r project_path; do
            project_path="$(echo "$project_path" | xargs)"
            if [[ -n "$project_path" ]]; then
                sln_projects+=("$project_path")
            fi
        done < <(printf '%s\n' "$projects_output" | tail -n +3)

        for project in "${sln_projects[@]}"; do
            echo "  dotnet format ${mode} --no-restore ${project}"
            if ! dotnet format "$mode" "$project" --no-restore; then
                format_failed=true
            fi
        done
    else
        echo "Running dotnet format ${description} across solution..."
        if ! dotnet format "$mode" --no-restore; then
            format_failed=true
        fi
    fi

    if [[ "$format_failed" == "true" ]]; then
        echo "dotnet format ${mode} encountered errors." >&2
    fi
}

echo "Fixing whitespace..."
"${ROOT_DIR}/utilities/fix-whitespace.sh"

run_dotnet_format_scope "style" "(style)"
run_dotnet_format_scope "analyzers" "(analyzers)"

echo "Building solution..."
dotnet build Webstir.sln -v minimal

echo "Done."

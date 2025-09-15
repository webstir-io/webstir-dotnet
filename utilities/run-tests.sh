#!/bin/bash

# Script directory
SCRIPT_DIR="$( cd "$( dirname "${BASH_SOURCE[0]}" )" && pwd )"
PROJECT_ROOT="$(dirname "$SCRIPT_DIR")"

# Change to project root
cd "$PROJECT_ROOT" || exit 1

# Run full test suite with spinner
echo -n "Running full test suite "

# Start spinner in background
(
    while true; do
        for s in '⠋' '⠙' '⠹' '⠸' '⠼' '⠴' '⠦' '⠧' '⠇' '⠏'; do
            echo -en "\r Running full test suite $s"
            sleep 0.1
        done
    done
) &

SPINNER_PID=$!

# Run tests
dotnet run --project Tests -- --full
TEST_EXIT=$?

# Stop spinner
kill $SPINNER_PID 2>/dev/null
echo -e "\r Running full test suite ✓"

exit $TEST_EXIT
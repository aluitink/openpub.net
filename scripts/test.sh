#!/bin/bash
set -euo pipefail

# Test runner script for ActivityPub.Core
# Usage: ./test.sh [Debug|Release] [test-filter]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

CONFIGURATION="${1:-Debug}"
TEST_PROJECT="${2:-$PROJECT_DIR/ActivityPub.Tests/ActivityPub.Tests.csproj}"
TEST_FILTER="${3:-}"

if [[ ! -f "$TEST_PROJECT" ]]; then
    echo "Error: Test project not found: $TEST_PROJECT"
    exit 1
fi

echo "=== Running ActivityPub.Tests ==="
echo "Configuration: $CONFIGURATION"
echo "Project: $TEST_PROJECT"

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore "$TEST_PROJECT"

# Run tests
if [[ -n "$TEST_FILTER" ]]; then
    echo "Running tests with filter: $TEST_FILTER"
    dotnet test "$TEST_PROJECT" --configuration "$CONFIGURATION" --no-build --filter "$TEST_FILTER"
else
    echo "Running all tests..."
    dotnet test "$TEST_PROJECT" --configuration "$CONFIGURATION" --no-build --collect:"XPlat Code Coverage"
fi

echo "=== Tests completed ==="

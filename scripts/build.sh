#!/bin/bash
set -euo pipefail

# Build script for ActivityPub.Core
# Usage: ./build.sh [Debug|Release] [path/to/project.csproj]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

CONFIGURATION="${1:-Debug}"
PROJECT_FILE="${2:-$PROJECT_DIR/src/ActivityPub.Core/ActivityPub.Core.csproj}"

echo "=== Building ActivityPub.Core ==="
echo "Configuration: $CONFIGURATION"
echo "Project: $PROJECT_FILE"

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore "$PROJECT_FILE"

# Build the project
echo "Building project..."
dotnet build "$PROJECT_FILE" --configuration "$CONFIGURATION" --no-restore

# Build tests project if exists
TEST_PROJECT="$PROJECT_DIR/src/ActivityPub.Tests/ActivityPub.Tests.csproj"
if [[ -f "$TEST_PROJECT" ]]; then
    echo "Building tests project..."
    dotnet build "$TEST_PROJECT" --configuration "$CONFIGURATION" --no-restore
fi

echo "=== Build completed successfully ==="

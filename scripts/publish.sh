#!/bin/bash
set -euo pipefail

# Publish script for ActivityPub.Core
# Usage: ./publish.sh [Debug|Release] [publish-dir]

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"

CONFIGURATION="${1:-Release}"
PUBLISH_DIR="${2:-$PROJECT_DIR/publish}"

PROJECT_FILE="$PROJECT_DIR/ActivityPub.Core/ActivityPub.Core.csproj"

echo "=== Publishing ActivityPub.Core ==="
echo "Configuration: $CONFIGURATION"
echo "Output: $PUBLISH_DIR"

# Restore dependencies
echo "Restoring dependencies..."
dotnet restore "$PROJECT_FILE"

# Clean previous publish
echo "Cleaning previous publish..."
rm -rf "$PUBLISH_DIR"
mkdir -p "$PUBLISH_DIR"

# Publish the project
echo "Publishing project..."
dotnet publish "$PROJECT_FILE" --configuration "$CONFIGURATION" --output "$PUBLISH_DIR" --no-restore

# Copy additional files
echo "Copying additional files..."
cp "$PROJECT_DIR/README.md" "$PUBLISH_DIR/" 2>/dev/null || true
cp "$PROJECT_DIR/LICENSE" "$PUBLISH_DIR/" 2>/dev/null || true

echo "=== Publish completed ==="
echo "Output directory: $PUBLISH_DIR"

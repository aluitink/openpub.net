#!/bin/bash
set -e

echo "Building ActivityPub Demo App..."

SCRIPT_DIR="$(cd "$(dirname "$0")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"

cd "$REPO_ROOT"

# Build Docker image with repo root as context
docker build -t activitypub-demo:latest -f SampleProjects/DemoApp/Dockerfile .

echo "Build complete. Run with: docker run -p 8080:8080 activitypub-demo:latest"

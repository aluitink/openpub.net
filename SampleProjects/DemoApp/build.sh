#!/bin/bash
set -e

echo "Building ActivityPub Demo App..."

cd "$(dirname "$0")"

# Build Docker image
docker build -t activitypub-demo:latest ../../

echo "Build complete. Run with: docker run -p 8080:8080 activitypub-demo:latest"

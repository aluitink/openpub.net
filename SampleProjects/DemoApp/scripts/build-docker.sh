#!/bin/bash
set -e

docker build -t activitypub-demo .
echo "Build complete. Run with: docker run -p 8080:8080 activitypub-demo"

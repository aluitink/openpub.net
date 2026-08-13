#!/bin/bash
set -e

echo "Starting ActivityPub Demo App..."

# Run Docker container
docker run -p 8080:8080 --rm activitypub-demo:latest

echo "Demo app stopped."

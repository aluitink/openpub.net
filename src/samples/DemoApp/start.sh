#!/bin/bash
set -e

echo "Starting ActivityPub Demo App with docker-compose..."

# Run with docker-compose
docker-compose up -d

echo "Demo app running on http://localhost:8080"
echo "View logs with: docker-compose logs -f"

#!/bin/bash
set -e

echo "ActivityPub Deployment Script"
echo "=============================="

# Configuration
DEPLOY_ENV=${DEPLOY_ENV:-production}
REGISTRY=${REGISTRY:-docker.io}
IMAGE_NAME=${IMAGE_NAME:-activitypub}
IMAGE_TAG=${IMAGE_TAG:-latest}

# Build Docker image
echo "Building Docker image..."
docker build -t ${REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG} .

# Push to registry
echo "Pushing to registry..."
docker push ${REGISTRY}/${IMAGE_NAME}:${IMAGE_TAG}

# Deploy to Kubernetes
if [ -f "kubernetes.yaml" ]; then
    echo "Deploying to Kubernetes..."
    kubectl apply -f kubernetes.yaml
fi

# Wait for deployment
echo "Waiting for deployment..."
kubectl rollout status deployment/activitypub

echo "Deployment complete!"
echo "Access the application at: http://activitypub.example.com"

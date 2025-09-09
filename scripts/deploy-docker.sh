#!/bin/bash

# ForgeFusion File Processing Service - Docker Deployment Script
# Usage: ./deploy-docker.sh [environment] [registry]

set -e

ENVIRONMENT=${1:-dev}
REGISTRY=${2:-ghcr.io/your-org/forgefusion.fileprocessing}
TAG=${3:-latest}

echo "?? Building and deploying ForgeFusion File Processing Service with Docker"
echo "Environment: $ENVIRONMENT"
echo "Registry: $REGISTRY"
echo "Tag: $TAG"

# Build images
echo "?? Building Docker images..."

# Build API image
echo "Building API image..."
docker build -f Dockerfile.api -t $REGISTRY/api:$TAG .

# Build Web image
echo "Building Web image..."
docker build -f Dockerfile.web -t $REGISTRY/web:$TAG .

# Push images to registry
echo "?? Pushing images to registry..."
docker push $REGISTRY/api:$TAG
docker push $REGISTRY/web:$TAG

# Deploy to Azure Container Instances (if environment variables are set)
if [ ! -z "$AZURE_RESOURCE_GROUP" ] && [ ! -z "$AZURE_LOCATION" ]; then
    echo "?? Deploying to Azure Container Instances..."
    
    # Create storage account for ACI
    STORAGE_ACCOUNT="forgefusionst${ENVIRONMENT}$(openssl rand -hex 4)"
    az storage account create \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --name "$STORAGE_ACCOUNT" \
        --location "$AZURE_LOCATION" \
        --sku Standard_LRS
    
    # Get storage connection string
    STORAGE_CONNECTION_STRING=$(az storage account show-connection-string \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --name "$STORAGE_ACCOUNT" \
        --query connectionString -o tsv)
    
    # Deploy API container
    az container create \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --name "forgefusion-api-$ENVIRONMENT" \
        --image "$REGISTRY/api:$TAG" \
        --dns-name-label "forgefusion-api-$ENVIRONMENT" \
        --ports 8080 \
        --environment-variables \
            ASPNETCORE_ENVIRONMENT="$ENVIRONMENT" \
            Storage__ConnectionString="$STORAGE_CONNECTION_STRING" \
            Storage__ContainerName="files" \
            Storage__QueueName="file-uploads" \
            Storage__TableName="fileProcessing" \
            Storage__AuditTableName="fileAudit" \
        --cpu 1 \
        --memory 1.5
    
    # Get API FQDN
    API_FQDN=$(az container show \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --name "forgefusion-api-$ENVIRONMENT" \
        --query ipAddress.fqdn -o tsv)
    
    # Deploy Web container
    az container create \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --name "forgefusion-web-$ENVIRONMENT" \
        --image "$REGISTRY/web:$TAG" \
        --dns-name-label "forgefusion-web-$ENVIRONMENT" \
        --ports 8080 \
        --environment-variables \
            ASPNETCORE_ENVIRONMENT="$ENVIRONMENT" \
            FileProcessingApi__BaseUrl="http://$API_FQDN:8080" \
        --cpu 1 \
        --memory 1.5
    
    # Get Web FQDN
    WEB_FQDN=$(az container show \
        --resource-group "$AZURE_RESOURCE_GROUP" \
        --name "forgefusion-web-$ENVIRONMENT" \
        --query ipAddress.fqdn -o tsv)
    
    echo "? Deployment completed successfully!"
    echo "?? Application URLs:"
    echo "API: http://$API_FQDN:8080"
    echo "Web: http://$WEB_FQDN:8080"
    echo "API Documentation: http://$API_FQDN:8080/scalar"
    
else
    echo "?? Azure deployment skipped. Set AZURE_RESOURCE_GROUP and AZURE_LOCATION to deploy to ACI."
    echo "? Docker images built and pushed successfully!"
    echo "?? You can now run the application locally with:"
    echo "docker-compose up -d"
fi

echo ""
echo "?? Images built:"
echo "API: $REGISTRY/api:$TAG"
echo "Web: $REGISTRY/web:$TAG"
#!/bin/bash

# ForgeFusion File Processing Service - Azure Deployment Script
# Usage: ./deploy.sh [environment] [resource-group-name] [subscription-id]

set -e

ENVIRONMENT=${1:-dev}
RESOURCE_GROUP=${2:-rg-forgefusion-$ENVIRONMENT}
SUBSCRIPTION_ID=${3}
LOCATION=${4:-eastus}

echo "?? Deploying ForgeFusion File Processing Service"
echo "Environment: $ENVIRONMENT"
echo "Resource Group: $RESOURCE_GROUP"
echo "Location: $LOCATION"

# Set Azure subscription if provided
if [ ! -z "$SUBSCRIPTION_ID" ]; then
    echo "Setting subscription to: $SUBSCRIPTION_ID"
    az account set --subscription "$SUBSCRIPTION_ID"
fi

# Create resource group if it doesn't exist
echo "?? Creating resource group..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

# Deploy infrastructure
echo "??? Deploying infrastructure..."
DEPLOYMENT_NAME="forgefusion-infrastructure-$(date +%Y%m%d-%H%M%S)"

az deployment group create \
    --resource-group "$RESOURCE_GROUP" \
    --template-file "infrastructure/main.bicep" \
    --parameters "@infrastructure/parameters.$ENVIRONMENT.json" \
    --name "$DEPLOYMENT_NAME" \
    --verbose

# Get deployment outputs
echo "?? Getting deployment outputs..."
STORAGE_ACCOUNT_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.storageAccountName.value -o tsv)
API_APP_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.apiAppName.value -o tsv)
WEB_APP_NAME=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.webAppName.value -o tsv)
API_APP_URL=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.apiAppUrl.value -o tsv)
WEB_APP_URL=$(az deployment group show --resource-group "$RESOURCE_GROUP" --name "$DEPLOYMENT_NAME" --query properties.outputs.webAppUrl.value -o tsv)

echo "? Infrastructure deployed successfully!"
echo ""
echo "?? Deployment Summary:"
echo "Storage Account: $STORAGE_ACCOUNT_NAME"
echo "API App: $API_APP_NAME"
echo "Web App: $WEB_APP_NAME"
echo "API URL: $API_APP_URL"
echo "Web URL: $WEB_APP_URL"
echo ""

# Check if we should deploy applications
if [ "$5" = "--skip-apps" ]; then
    echo "?? Skipping application deployment"
    exit 0
fi

# Build and deploy applications
echo "?? Building applications..."

# Build API
echo "Building API..."
dotnet publish ForgeFusion.Fileprocessing.Api/ForgeFusion.Fileprocessing.Api.csproj \
    --configuration Release \
    --output ./publish/api

# Build Web
echo "Building Web..."
dotnet publish ForgeFusion.Fileprocessing.Web/ForgeFusion.Fileprocessing.Web.csproj \
    --configuration Release \
    --output ./publish/web

# Create deployment packages
echo "?? Creating deployment packages..."
cd ./publish/api && zip -r ../api.zip . && cd ../..
cd ./publish/web && zip -r ../web.zip . && cd ../..

# Deploy API
echo "?? Deploying API..."
az webapp deployment source config-zip \
    --resource-group "$RESOURCE_GROUP" \
    --name "$API_APP_NAME" \
    --src "./publish/api.zip"

# Deploy Web
echo "?? Deploying Web..."
az webapp deployment source config-zip \
    --resource-group "$RESOURCE_GROUP" \
    --name "$WEB_APP_NAME" \
    --src "./publish/web.zip"

# Warm up applications
echo "?? Warming up applications..."
sleep 30

curl -f "$API_APP_URL/api/files" || echo "?? API warmup failed"
curl -f "$WEB_APP_URL" || echo "?? Web warmup failed"

# Cleanup
echo "?? Cleaning up..."
rm -rf ./publish

echo ""
echo "?? Deployment completed successfully!"
echo ""
echo "?? Application URLs:"
echo "API: $API_APP_URL"
echo "Web: $WEB_APP_URL"
echo "API Documentation: $API_APP_URL/scalar"
echo ""
echo "?? Next steps:"
echo "1. Test the applications using the URLs above"
echo "2. Configure custom domains if needed"
echo "3. Set up monitoring alerts"
echo "4. Review security settings"
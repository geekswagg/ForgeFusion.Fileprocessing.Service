# Deployment Guide

This guide covers all deployment options for the ForgeFusion File Processing Service.

## Deployment Options

1. **Azure App Service** (Recommended for production)
2. **Docker Containers** (Azure Container Instances or Kubernetes)
3. **Manual Deployment** (Development and testing)

## Prerequisites

### Azure Account Setup
- Active Azure subscription
- Azure CLI installed and configured
- Appropriate permissions (Contributor role minimum)

### Required Tools
- [Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli)
- [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- [Docker](https://www.docker.com/products/docker-desktop) (for container deployments)

## ?? Quick Deployment (Azure App Service)

### Using Deployment Script (Recommended)

```bash
# Make script executable (Linux/Mac)
chmod +x scripts/deploy.sh

# Deploy to development environment
./scripts/deploy.sh dev rg-forgefusion-dev

# Deploy to production environment
./scripts/deploy.sh prod rg-forgefusion-prod <your-subscription-id>
```

**Windows:**
```cmd
scripts\deploy.bat dev rg-forgefusion-dev
```

### Manual Azure App Service Deployment

#### Step 1: Create Azure Resources

```bash
# Login to Azure
az login

# Set subscription (if you have multiple)
az account set --subscription "your-subscription-id"

# Create resource group
az group create --name rg-forgefusion-prod --location eastus

# Deploy infrastructure using Bicep
az deployment group create \
    --resource-group rg-forgefusion-prod \
    --template-file infrastructure/main.bicep \
    --parameters @infrastructure/parameters.prod.json
```

#### Step 2: Build Applications

```bash
# Build API
dotnet publish ForgeFusion.Fileprocessing.Api/ForgeFusion.Fileprocessing.Api.csproj \
    --configuration Release \
    --output ./publish/api

# Build Web
dotnet publish ForgeFusion.Fileprocessing.Web/ForgeFusion.Fileprocessing.Web.csproj \
    --configuration Release \
    --output ./publish/web
```

#### Step 3: Deploy to Azure

```bash
# Get app names from deployment
API_APP_NAME=$(az deployment group show --resource-group rg-forgefusion-prod --name <deployment-name> --query properties.outputs.apiAppName.value -o tsv)
WEB_APP_NAME=$(az deployment group show --resource-group rg-forgefusion-prod --name <deployment-name> --query properties.outputs.webAppName.value -o tsv)

# Create deployment packages
cd ./publish/api && zip -r ../api.zip . && cd ../..
cd ./publish/web && zip -r ../web.zip . && cd ../..

# Deploy API
az webapp deployment source config-zip \
    --resource-group rg-forgefusion-prod \
    --name $API_APP_NAME \
    --src ./publish/api.zip

# Deploy Web
az webapp deployment source config-zip \
    --resource-group rg-forgefusion-prod \
    --name $WEB_APP_NAME \
    --src ./publish/web.zip
```

## ?? Docker Deployment

### Azure Container Instances

```bash
# Build and deploy with Docker script
chmod +x scripts/deploy-docker.sh
export AZURE_RESOURCE_GROUP=rg-forgefusion-containers
export AZURE_LOCATION=eastus

./scripts/deploy-docker.sh prod ghcr.io/your-org/forgefusion
```

### Manual Container Deployment

```bash
# Build images
docker build -f Dockerfile.api -t forgefusion-api:latest .
docker build -f Dockerfile.web -t forgefusion-web:latest .

# Run with Docker Compose
docker-compose up -d

# Or run individual containers
docker run -d -p 5000:8080 \
    -e Storage__ConnectionString="your-connection-string" \
    forgefusion-api:latest

docker run -d -p 5001:8080 \
    -e FileProcessingApi__BaseUrl="http://api:8080" \
    forgefusion-web:latest
```

### Kubernetes Deployment

Create Kubernetes manifests:

```yaml
# k8s/namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: forgefusion
---
# k8s/configmap.yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: forgefusion-config
  namespace: forgefusion
data:
  Storage__ContainerName: "files"
  Storage__QueueName: "file-uploads"
  Storage__TableName: "fileProcessing"
  Storage__AuditTableName: "fileAudit"
---
# k8s/secret.yaml
apiVersion: v1
kind: Secret
metadata:
  name: forgefusion-secrets
  namespace: forgefusion
type: Opaque
data:
  storage-connection-string: <base64-encoded-connection-string>
---
# k8s/api-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: forgefusion-api
  namespace: forgefusion
spec:
  replicas: 2
  selector:
    matchLabels:
      app: forgefusion-api
  template:
    metadata:
      labels:
        app: forgefusion-api
    spec:
      containers:
      - name: api
        image: ghcr.io/your-org/forgefusion/api:latest
        ports:
        - containerPort: 8080
        env:
        - name: Storage__ConnectionString
          valueFrom:
            secretKeyRef:
              name: forgefusion-secrets
              key: storage-connection-string
        envFrom:
        - configMapRef:
            name: forgefusion-config
        livenessProbe:
          httpGet:
            path: /api/files
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 30
        readinessProbe:
          httpGet:
            path: /api/files
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
---
# k8s/api-service.yaml
apiVersion: v1
kind: Service
metadata:
  name: forgefusion-api-service
  namespace: forgefusion
spec:
  selector:
    app: forgefusion-api
  ports:
  - protocol: TCP
    port: 80
    targetPort: 8080
  type: LoadBalancer
```

Deploy to Kubernetes:
```bash
kubectl apply -f k8s/
```

## Environment Configuration

### Development Environment
```json
{
  "environmentName": "dev",
  "appServicePlanSku": "B1",
  "storageAccountSku": "Standard_LRS"
}
```

### Staging Environment
```json
{
  "environmentName": "staging", 
  "appServicePlanSku": "S1",
  "storageAccountSku": "Standard_GRS"
}
```

### Production Environment
```json
{
  "environmentName": "prod",
  "appServicePlanSku": "P1V2",
  "storageAccountSku": "Standard_GRS"
}
```

## Configuration Management

### Azure App Configuration (Recommended for Production)

```bash
# Create App Configuration
az appconfig create \
    --name forgefusion-config-prod \
    --resource-group rg-forgefusion-prod \
    --location eastus \
    --sku Standard

# Add configuration values
az appconfig kv set \
    --name forgefusion-config-prod \
    --key "Storage:MaxFileSize" \
    --value "209715200"

# Configure apps to use App Configuration
az webapp config appsettings set \
    --name $API_APP_NAME \
    --resource-group rg-forgefusion-prod \
    --settings ConnectionStrings__AppConfig="connection-string-here"
```

### Key Vault Integration

```bash
# Create Key Vault
az keyvault create \
    --name forgefusion-kv-prod \
    --resource-group rg-forgefusion-prod \
    --location eastus

# Add secrets
az keyvault secret set \
    --vault-name forgefusion-kv-prod \
    --name "StorageConnectionString" \
    --value "your-storage-connection-string"

# Configure managed identity and access
az webapp identity assign \
    --name $API_APP_NAME \
    --resource-group rg-forgefusion-prod

# Grant access to Key Vault
az keyvault set-policy \
    --name forgefusion-kv-prod \
    --object-id $(az webapp identity show --name $API_APP_NAME --resource-group rg-forgefusion-prod --query principalId -o tsv) \
    --secret-permissions get list
```

## Monitoring and Logging

### Application Insights Setup

Automatically configured in Bicep templates. Manual setup:

```bash
# Create Application Insights
az monitor app-insights component create \
    --app forgefusion-ai-prod \
    --location eastus \
    --resource-group rg-forgefusion-prod \
    --application-type web

# Configure applications
az webapp config appsettings set \
    --name $API_APP_NAME \
    --resource-group rg-forgefusion-prod \
    --settings APPLICATIONINSIGHTS_CONNECTION_STRING="connection-string-here"
```

### Custom Metrics and Alerts

```bash
# Create metric alert for high error rate
az monitor metrics alert create \
    --name "High Error Rate - API" \
    --resource-group rg-forgefusion-prod \
    --scopes "/subscriptions/<subscription-id>/resourceGroups/rg-forgefusion-prod/providers/Microsoft.Web/sites/$API_APP_NAME" \
    --condition "avg requests/failed > 10" \
    --description "Alert when API error rate is high"
```

## Security Configuration

### HTTPS and SSL
- Automatic HTTPS redirection configured
- SSL certificates managed by Azure
- Custom domains supported

### Authentication (Optional)
Configure Azure AD B2C for authentication:

```bash
# Create Azure AD B2C tenant
# Configure authentication in application settings
az webapp auth config-version upgrade --name $WEB_APP_NAME --resource-group rg-forgefusion-prod
az webapp auth microsoft update \
    --name $WEB_APP_NAME \
    --resource-group rg-forgefusion-prod \
    --client-id "your-client-id" \
    --client-secret "your-client-secret" \
    --issuer "https://login.microsoftonline.com/your-tenant-id/v2.0"
```

### Network Security
```bash
# Configure IP restrictions
az webapp config access-restriction add \
    --name $API_APP_NAME \
    --resource-group rg-forgefusion-prod \
    --rule-name "AllowWebApp" \
    --action Allow \
    --ip-address "10.0.0.0/8"
```

## Performance Optimization

### App Service Configuration
```bash
# Enable Always On (for production)
az webapp config set \
    --name $API_APP_NAME \
    --resource-group rg-forgefusion-prod \
    --always-on true

# Configure scaling
az monitor autoscale create \
    --resource-group rg-forgefusion-prod \
    --resource $API_APP_NAME \
    --resource-type Microsoft.Web/sites \
    --name autoscale-api \
    --min-count 1 \
    --max-count 10 \
    --count 2
```

### Storage Optimization
- Use appropriate storage tier (Hot for frequently accessed files)
- Configure lifecycle management for automatic archival
- Enable storage analytics and monitoring

## Backup and Disaster Recovery

### Database Backup
```bash
# Configure automatic backups for App Service
az webapp config backup update \
    --resource-group rg-forgefusion-prod \
    --webapp-name $API_APP_NAME \
    --storage-account-url "storage-url" \
    --frequency 1 \
    --retention 30
```

### Storage Backup
- Configure geo-redundant storage (GRS)
- Set up cross-region replication
- Implement backup policies

## Troubleshooting

### Common Deployment Issues

#### Application won't start
1. Check application logs:
   ```bash
   az webapp log tail --name $API_APP_NAME --resource-group rg-forgefusion-prod
   ```

2. Verify configuration:
   ```bash
   az webapp config appsettings list --name $API_APP_NAME --resource-group rg-forgefusion-prod
   ```

#### Storage connection issues
1. Verify connection string format
2. Check firewall rules
3. Validate access permissions

#### Performance issues
1. Monitor Application Insights metrics
2. Check App Service Plan capacity
3. Review storage performance metrics

### Rollback Procedures

#### App Service Rollback
```bash
# List deployment slots
az webapp deployment slot list --name $API_APP_NAME --resource-group rg-forgefusion-prod

# Swap slots to rollback
az webapp deployment slot swap \
    --slot staging \
    --name $API_APP_NAME \
    --resource-group rg-forgefusion-prod
```

#### Container Rollback
```bash
# Roll back to previous image version
kubectl set image deployment/forgefusion-api api=ghcr.io/your-org/forgefusion/api:previous-tag
```

## Post-Deployment Checklist

- [ ] Verify applications are accessible
- [ ] Test file upload/download functionality
- [ ] Check monitoring and alerting
- [ ] Validate SSL certificates
- [ ] Review security settings
- [ ] Test backup procedures
- [ ] Update DNS records (if using custom domains)
- [ ] Monitor performance metrics
- [ ] Verify log collection
- [ ] Test disaster recovery procedures

## Maintenance

### Regular Tasks
- Monitor Application Insights dashboards
- Review and rotate secrets
- Update dependencies and security patches
- Review and optimize costs
- Backup verification
- Performance tuning

### Scaling Considerations
- Monitor resource utilization
- Configure auto-scaling rules
- Plan for traffic spikes
- Consider multiple regions for global reach

---

For additional help with deployment, please refer to:
- [Azure Documentation](https://docs.microsoft.com/en-us/azure/)
- [GitHub Issues](https://github.com/your-org/ForgeFusion.Fileprocessing/issues)
- [Project README](../README.md)
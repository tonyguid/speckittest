# Deployment Guide

Complete guide for deploying the Blob Copy application to development and production environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Deployment Overview](#deployment-overview)
3. [Local Development Setup](#local-development-setup)
4. [Azure Deployment with Terraform](#azure-deployment-with-terraform)
5. [CI/CD Pipeline](#cicd-pipeline)
6. [Environment Configuration](#environment-configuration)
7. [Monitoring & Logging](#monitoring--logging)
8. [Troubleshooting](#troubleshooting)
9. [Post-Deployment Checklist](#post-deployment-checklist)

---

## Prerequisites

### Required Tools

- **Azure Subscription** with appropriate permissions
- **Azure CLI** version 2.40+ (`az` command)
- **Terraform** version 1.0+ for infrastructure deployment
- **.NET SDK** version 10.0+
- **Node.js** version 18.x+ (recommended: 20.x)
- **Git** for version control

### Development Tools (Recommended)

- **Visual Studio Code** with Azure extensions
- **Azure Storage Explorer** for blob management
- **Postman** or similar for API testing

### Azure Permissions Required

- Contributor access to Azure subscription
- Ability to create:
  - Resource Groups
  - App Service Plans
  - Static Web Apps
  - Storage Accounts
  - Key Vaults
  - Application Insights
  - User Assigned Managed Identities

---

## Deployment Overview

### Current Architecture

The Blob Copy application uses the following Azure services:

| Service | Purpose | Configuration |
|---------|---------|---------------|
| **Azure Static Web App** | Frontend hosting (React/Vite) | Managed in Terraform |
| **Azure App Service** | Backend API (.NET 10) | Linux App Service, B2 SKU |
| **Azure Storage Account** | Blob storage for copy operations | Standard LRS |
| **Azure Key Vault** | Secrets management | Stores connection strings |
| **Application Insights** | Telemetry and monitoring | 30-day retention |
| **User Managed Identity** | Backend authentication | Access to Storage/Key Vault |

### Deployment Methods

| Method | Use Case | Complexity |
|--------|----------|------------|
| **Local Development** | Development & testing | Low |
| **Terraform (Recommended)** | Production deployment | Medium |
| **GitHub Actions** | Automated CI/CD | Low (automated) |

---

## Local Development Setup

### 1. Clone Repository

```bash
git clone https://github.com/your-org/blob-copy.git
cd blob-copy
```

### 2. Backend Setup

#### Install Azure Storage Emulator (Azurite)

```bash
# Install Azurite globally
npm install -g azurite

# Start Azurite in a separate terminal
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

#### Configure Backend

The backend is pre-configured for local development in `src/backend/BlobCopy.API/appsettings.Development.json`:

```json
{
  "Azure": {
    "Storage": {
      "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...",
      "AccountUri": "http://127.0.0.1:10000/devstoreaccount1"
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000"]
  }
}
```

#### Run Backend

```bash
cd src/backend

# Restore dependencies
dotnet restore

# Run the API (starts on http://localhost:5000)
dotnet run --project BlobCopy.API
```

#### Verify Backend

```bash
# Health check
curl http://localhost:5000/health

# Should return: {"status":"Healthy"}
```

### 3. Frontend Setup

```bash
cd src/frontend

# Install dependencies
npm install

# Start development server (runs on http://localhost:5173)
npm run dev
```

The frontend automatically uses these environment defaults for local development:
- API URL: `http://localhost:5000`
- SignalR Hub: `http://localhost:5000/blobcopyhub`

#### Verify Frontend

Open your browser to `http://localhost:5173` - you should see the Blob Copy UI.

### 4. Running Tests

#### Backend Tests

```bash
cd src/backend

# Run all tests
dotnet test

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

#### Frontend Tests

```bash
cd src/frontend

# Unit tests
npm test

# Coverage
npm run test:coverage

# E2E tests (requires backend running)
npm run test:e2e
```

---

## Azure Deployment with Terraform

### Infrastructure Overview

The Terraform configuration (`infra/terraform/main.tf`) provisions all required Azure resources with a single deployment.

### 1. Prepare Azure Credentials

```bash
# Login to Azure
az login

# Set your subscription
az account set --subscription "Your Subscription Name"

# Verify current subscription
az account show --query "[name,id]"
```

### 2. Configure Terraform Variables

Create a `terraform.tfvars` file in the `infra/terraform` directory:

```hcl
# infra/terraform/terraform.tfvars
location            = "eastus"
environment         = "prod"
app_name            = "blob-copy"
app_service_sku     = "B2"
app_insights_retention_days = 30
```

**Available SKUs:**
- `B1` - Basic ($13/month) - Dev/test
- `B2` - Basic ($26/month) - Small production
- `S1` - Standard ($70/month) - Production with auto-scale
- `P1V2` - Premium ($146/month) - High performance

### 3. Initialize Terraform

```bash
cd infra/terraform

# Initialize Terraform
terraform init

# Validate configuration
terraform validate

# Preview changes
terraform plan
```

### 4. Deploy Infrastructure

```bash
# Apply configuration
terraform apply

# Review the plan and type 'yes' to confirm
```

**Deployment Time:** Approximately 5-10 minutes

### 5. Configure Backend Application

After Terraform completes, configure the backend app settings:

```bash
# Get outputs from Terraform
STORAGE_CONN_STRING=$(terraform output -raw storage_connection_string 2>/dev/null || az storage account show-connection-string --name $(terraform output -raw storage_account_name) --resource-group $(terraform output -raw resource_group_name) --query connectionString -o tsv)

APP_INSIGHTS_KEY=$(terraform output -raw app_insights_instrumentation_key)

# Update App Service settings
az webapp config appsettings set \
  --name $(terraform output -raw app_service_name) \
  --resource-group $(terraform output -raw resource_group_name) \
  --settings \
    Azure__Storage__ConnectionString="$STORAGE_CONN_STRING" \
    ApplicationInsights__InstrumentationKey="$APP_INSIGHTS_KEY"
```

### 6. Deploy Backend Code

```bash
cd ../../src/backend

# Build and publish
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../api-deploy.zip .

# Deploy to App Service
az webapp deployment source config-zip \
  --src ../api-deploy.zip \
  --name $(cd ../../infra/terraform && terraform output -raw app_service_name) \
  --resource-group $(cd ../../infra/terraform && terraform output -raw resource_group_name)
```

### 7. Deploy Frontend to Static Web App

```bash
cd src/frontend

# Set production environment variables
export VITE_API_URL=$(cd ../../infra/terraform && terraform output -raw app_service_url)
export VITE_SIGNALR_URL="$VITE_API_URL/blobcopyhub"

# Build for production
npm run build

# Deploy to Static Web App
az staticwebapp upload \
  --name $(cd ../../infra/terraform && terraform output -raw static_web_app_name) \
  --resource-group $(cd ../../infra/terraform && terraform output -raw resource_group_name) \
  --app-location ./dist \
  --output-location ./dist
```

### 8. Verify Deployment

```bash
# Get application URLs
terraform output static_web_app_url
terraform output app_service_url

# Test backend health
curl $(terraform output -raw app_service_url)/health

# Open frontend in browser
open $(terraform output -raw static_web_app_url)
```

### Infrastructure Management

#### View Current State

```bash
cd infra/terraform
terraform show
terraform output
```

#### Update Infrastructure

```bash
# Modify terraform.tfvars or main.tf
terraform plan
terraform apply
```

#### Destroy Infrastructure

```bash
# WARNING: This will delete all resources
terraform destroy
```

---

## CI/CD Pipeline

The project includes GitHub Actions workflows for automated testing and deployment.

### Workflows

#### 1. CI/CD Pipeline (`.github/workflows/ci.yml`)

**Triggers:**
- Push to `main` or `develop` branches
- Pull requests to `main` or `develop`

**Jobs:**
- **Backend Build & Test**: Builds .NET API, runs unit/integration tests, uploads coverage
- **Frontend Build & Test**: Builds React app, runs unit tests with Vitest, uploads coverage
- **E2E Tests**: Runs Playwright end-to-end tests
- **Code Quality**: Runs linters and formatters
- **Docker Build**: Builds and pushes Docker images (main branch only)

#### 2. E2E Tests (`.github/workflows/e2e-tests.yml`)

Runs comprehensive end-to-end tests including:
- Blob copy operations
- Conflict resolution flows
- Progress tracking
- Error handling

#### 3. Performance Tests (`.github/workflows/performance-tests.yml`)

**Schedule:** Nightly at 2 AM UTC

**Tests:**
- Backend API performance benchmarks
- Frontend bundle size analysis
- Lighthouse performance audits
- Regression detection

### Setting Up CI/CD

#### Required GitHub Secrets

Add these secrets to your GitHub repository (`Settings > Secrets and variables > Actions`):

```
# Docker Hub (optional - for Docker image builds)
DOCKER_USERNAME=your-docker-username
DOCKER_PASSWORD=your-docker-password

# Azure Credentials (for deployment)
AZURE_CREDENTIALS='{
  "clientId": "xxx",
  "clientSecret": "xxx",
  "subscriptionId": "xxx",
  "tenantId": "xxx"
}'

# Azure Resource Information
AZURE_RESOURCE_GROUP=blob-copy-prod-rg
AZURE_APP_SERVICE_NAME=blob-copy-prod-api
AZURE_STATIC_WEB_APP_NAME=blob-copy-prod-swa
```

#### Create Azure Service Principal

```bash
# Create service principal for GitHub Actions
az ad sp create-for-rbac \
  --name "github-actions-blob-copy" \
  --role contributor \
  --scopes /subscriptions/{subscription-id}/resourceGroups/{resource-group} \
  --sdk-auth

# Copy the JSON output to AZURE_CREDENTIALS secret
```

#### Automated Deployment Workflow

To enable automated deployment on merge to main:

1. Create `.github/workflows/deploy-prod.yml`:

```yaml
name: Deploy to Production

on:
  push:
    branches: [ main ]

jobs:
  deploy:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3

      - name: Azure Login
        uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}

      - name: Deploy Backend
        run: |
          cd src/backend
          dotnet publish -c Release -o ./publish
          cd publish && zip -r ../deploy.zip .
          az webapp deployment source config-zip \
            --src ../deploy.zip \
            --name ${{ secrets.AZURE_APP_SERVICE_NAME }} \
            --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }}

      - name: Deploy Frontend
        run: |
          cd src/frontend
          npm ci
          npm run build
          az staticwebapp upload \
            --name ${{ secrets.AZURE_STATIC_WEB_APP_NAME }} \
            --resource-group ${{ secrets.AZURE_RESOURCE_GROUP }} \
            --app-location ./dist
```

---

## Environment Configuration

### Backend Configuration

#### Production (`appsettings.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    }
  },
  "Azure": {
    "Storage": {
      "AccountUri": "https://{storage-account}.blob.core.windows.net"
    },
    "Authentication": {
      "TenantId": "YOUR_TENANT_ID",
      "ClientId": "YOUR_CLIENT_ID",
      "Audience": "api://blob-copy-api"
    }
  },
  "ApplicationInsights": {
    "InstrumentationKey": "YOUR_INSTRUMENTATION_KEY"
  },
  "Cors": {
    "AllowedOrigins": [
      "https://{static-web-app}.azurestaticapps.net"
    ]
  },
  "AllowedHosts": "{your-domain}.com"
}
```

#### Development (`appsettings.Development.json`)

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "BlobCopy": "Debug"
    }
  },
  "Azure": {
    "Storage": {
      "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;...",
      "AccountUri": "http://127.0.0.1:10000/devstoreaccount1"
    }
  },
  "Cors": {
    "AllowedOrigins": ["http://localhost:3000", "http://localhost:5173"]
  },
  "AllowedHosts": "*"
}
```

### Frontend Configuration

Frontend uses Vite environment variables:

#### Production

Set via App Service configuration or Static Web App settings:

```bash
VITE_API_URL=https://blob-copy-prod-api.azurewebsites.net
VITE_SIGNALR_URL=https://blob-copy-prod-api.azurewebsites.net/blobcopyhub
VITE_LOG_LEVEL=error
```

#### Development

Defaults are configured in the app:

```javascript
// Uses localhost:5000 by default
const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000';
const SIGNALR_URL = import.meta.env.VITE_SIGNALR_URL || 'http://localhost:5000/blobcopyhub';
```

---

## Monitoring & Logging

### Application Insights

The Terraform deployment automatically provisions Application Insights.

#### View Logs

```bash
# Get Application Insights app ID
az monitor app-insights component show \
  --app $(cd infra/terraform && terraform output -raw app_insights_name) \
  --resource-group $(cd infra/terraform && terraform output -raw resource_group_name) \
  --query appId -o tsv

# Query logs
az monitor app-insights query \
  --app {app-id} \
  --analytics-query "requests | where timestamp > ago(1h) | summarize count() by resultCode"
```

#### Key Metrics to Monitor

- **API Response Time**: Target p95 < 2000ms
- **Error Rate**: Target < 1%
- **SignalR Connections**: Monitor for connection drops
- **Blob Copy Operations**: Track success/failure rates
- **Memory Usage**: Alert if > 80%
- **CPU Usage**: Alert if > 70%

### Create Alerts

```bash
# Alert on high error rate
az monitor metrics alert create \
  --name blob-copy-high-errors \
  --resource-group $(cd infra/terraform && terraform output -raw resource_group_name) \
  --scopes $(cd infra/terraform && terraform output -raw app_service_id) \
  --condition "avg requests/failed > 10" \
  --window-size 5m \
  --evaluation-frequency 1m \
  --action email your-email@example.com
```

### View App Service Logs

```bash
# Stream logs in real-time
az webapp log tail \
  --name $(cd infra/terraform && terraform output -raw app_service_name) \
  --resource-group $(cd infra/terraform && terraform output -raw resource_group_name)

# Download logs
az webapp log download \
  --name $(cd infra/terraform && terraform output -raw app_service_name) \
  --resource-group $(cd infra/terraform && terraform output -raw resource_group_name) \
  --log-file app-logs.zip
```

---

## Troubleshooting

### Common Issues

#### 1. "Azure Credentials Not Found"

**Error:**
```
ManagedIdentityCredential authentication unavailable
```

**Solution:**
For local development, use Azure CLI authentication:
```bash
az login
az account set --subscription "Your Subscription"
```

For production, ensure Managed Identity is assigned and has proper roles:
```bash
# Grant Storage Blob Data Contributor role
az role assignment create \
  --assignee $(cd infra/terraform && terraform output -raw backend_identity_principal_id) \
  --role "Storage Blob Data Contributor" \
  --scope $(cd infra/terraform && terraform output -raw storage_account_id)
```

#### 2. CORS Errors

**Error:**
```
Access to fetch at 'https://api.example.com' from origin 'https://app.example.com'
has been blocked by CORS policy
```

**Solution:**
Update CORS settings in `appsettings.json`:
```json
{
  "Cors": {
    "AllowedOrigins": [
      "https://your-static-web-app.azurestaticapps.net",
      "https://your-custom-domain.com"
    ]
  }
}
```

Redeploy the backend after updating.

#### 3. SignalR Connection Fails

**Error:**
```
WebSocket connection to 'wss://...' failed
```

**Solutions:**
- Verify SignalR URL is correct (should use `wss://` for HTTPS)
- Check App Service supports WebSockets (enabled by default)
- Verify CORS includes SignalR hub endpoint
- Check firewall allows WebSocket connections on port 443

#### 4. Slow Blob Copy Performance

**Symptoms:**
- Copy operations slower than expected
- Timeouts on large files

**Solutions:**
1. Check network bandwidth between regions
2. Verify storage account is in same region as App Service
3. Monitor App Service CPU/memory usage
4. Consider scaling up App Service plan:
   ```bash
   az appservice plan update \
     --name $(cd infra/terraform && terraform output -raw app_service_plan_name) \
     --resource-group $(cd infra/terraform && terraform output -raw resource_group_name) \
     --sku S1
   ```

#### 5. Frontend Build Fails

**Error:**
```
Module not found: Can't resolve '@microsoft/signalr'
```

**Solution:**
```bash
cd src/frontend
rm -rf node_modules package-lock.json
npm install
npm run build
```

### Debug Commands

```bash
# Check API health
curl https://your-app-service.azurewebsites.net/health

# Test storage account connectivity
az storage blob list \
  --account-name $(cd infra/terraform && terraform output -raw storage_account_name) \
  --container-name test \
  --auth-mode login

# Verify App Service configuration
az webapp config appsettings list \
  --name $(cd infra/terraform && terraform output -raw app_service_name) \
  --resource-group $(cd infra/terraform && terraform output -raw resource_group_name)

# Check Static Web App status
az staticwebapp show \
  --name $(cd infra/terraform && terraform output -raw static_web_app_name) \
  --resource-group $(cd infra/terraform && terraform output -raw resource_group_name)
```

---

## Post-Deployment Checklist

### Functional Testing

- [ ] Frontend loads without errors at Static Web App URL
- [ ] Backend health endpoint returns 200 OK
- [ ] Form validation works correctly
- [ ] Can successfully copy a small blob (< 10MB)
- [ ] Can successfully copy a large blob (> 100MB)
- [ ] Progress updates display correctly during copy
- [ ] Cancellation functionality works
- [ ] Overwrite confirmation dialog appears for existing blobs
- [ ] Error messages display appropriately for failures
- [ ] SignalR real-time updates work

### Performance Verification

- [ ] API response time < 2s for validation endpoints
- [ ] API response time < 5s for copy initiation
- [ ] Frontend initial load < 3s
- [ ] No console errors in browser
- [ ] WebSocket connection establishes successfully

### Security Verification

- [ ] HTTPS is enforced (no HTTP access)
- [ ] CORS is restricted to known origins only
- [ ] Managed Identity authentication is working
- [ ] No secrets or connection strings in code/logs
- [ ] Key Vault secrets are encrypted at rest
- [ ] App Service authentication is configured (if required)
- [ ] Input validation prevents injection attacks
- [ ] Rate limiting is in place (if implemented)

### Monitoring Setup

- [ ] Application Insights is receiving telemetry
- [ ] Log queries return expected results
- [ ] Alerts are configured for critical metrics
- [ ] Dashboard is created in Azure Portal
- [ ] On-call team has access to monitoring tools
- [ ] Alert contact information is correct

### Operational Readiness

- [ ] Deployment documentation is up to date
- [ ] Runbook exists for common issues
- [ ] Rollback procedure is documented and tested
- [ ] Team is trained on deployment process
- [ ] Emergency contacts are documented
- [ ] Backup strategy is implemented (if applicable)

### Cost Optimization

- [ ] Review actual resource usage vs. provisioned capacity
- [ ] Consider reserved instances for production
- [ ] Set up budget alerts in Azure Cost Management
- [ ] Review and remove unused resources
- [ ] Verify auto-scaling is configured appropriately

---

## Maintenance

### Regular Tasks

**Daily:**
- Monitor Application Insights for errors and performance degradation
- Review automated test results from CI/CD pipeline
- Check storage account usage and costs

**Weekly:**
- Review security advisories for .NET and npm packages
- Update dependencies with security patches
- Review and clean up old logs (if using custom logging)
- Test backup and restore procedures

**Monthly:**
- Review Application Insights retention policies
- Analyze cost trends and optimize resources
- Update documentation for any process changes
- Conduct security review of access policies
- Test disaster recovery procedures

**Quarterly:**
- Major version upgrades for .NET and Node.js
- Review and update Terraform modules
- Comprehensive security audit
- Capacity planning review
- Team training on new features

### Updating Dependencies

#### Backend

```bash
cd src/backend

# Check for outdated packages
dotnet list package --outdated

# Update specific package
dotnet add package Azure.Storage.Blobs --version {version}

# Run tests after update
dotnet test
```

#### Frontend

```bash
cd src/frontend

# Check for outdated packages
npm outdated

# Update specific package
npm install @microsoft/signalr@latest

# Update all packages (use with caution)
npm update

# Run tests after update
npm test
npm run test:e2e
```

### Scaling Strategy

#### Vertical Scaling (Increase Resources)

```bash
# Scale up App Service
az appservice plan update \
  --name blob-copy-prod-asp \
  --resource-group blob-copy-prod-rg \
  --sku S2

# Takes effect immediately
```

#### Horizontal Scaling (Add Instances)

```bash
# Scale out to 3 instances
az appservice plan update \
  --name blob-copy-prod-asp \
  --resource-group blob-copy-prod-rg \
  --number-of-workers 3
```

#### Auto-Scaling Configuration

```bash
# Enable autoscale (requires Standard tier or higher)
az monitor autoscale create \
  --resource-group blob-copy-prod-rg \
  --resource $(cd infra/terraform && terraform output -raw app_service_plan_id) \
  --name blob-copy-autoscale \
  --min-count 2 \
  --max-count 5 \
  --count 2

# Scale up on high CPU
az monitor autoscale rule create \
  --resource-group blob-copy-prod-rg \
  --autoscale-name blob-copy-autoscale \
  --condition "Percentage CPU > 70 avg 5m" \
  --scale out 1

# Scale down on low CPU
az monitor autoscale rule create \
  --resource-group blob-copy-prod-rg \
  --autoscale-name blob-copy-autoscale \
  --condition "Percentage CPU < 30 avg 10m" \
  --scale in 1
```

---

## Cost Optimization

### Current Cost Estimates

**Development Environment:**
- App Service (B1): ~$13/month
- Storage Account: ~$10/month
- Application Insights: Free tier
- Static Web App: Free tier
- **Total: ~$25/month**

**Production Environment (B2 SKU):**
- App Service (B2, 2 instances): ~$52/month
- Storage Account (Standard LRS): ~$20/month
- Application Insights (30-day retention): ~$10/month
- Static Web App (Standard): ~$9/month
- Key Vault: ~$3/month
- **Total: ~$95/month**

**Production Environment (S1 SKU with Auto-scale):**
- App Service (S1, avg 3 instances): ~$210/month
- Storage Account: ~$30/month
- Application Insights: ~$50/month
- Static Web App (Standard): ~$9/month
- Key Vault: ~$3/month
- **Total: ~$300/month**

### Cost Reduction Strategies

1. **Use Azurite for Development**
   - Eliminates storage costs during development
   - Already configured in `appsettings.Development.json`

2. **Right-Size App Service Plan**
   - Monitor actual CPU/memory usage
   - Start with B2, scale up only if needed
   - Development can use B1 or Free tier

3. **Configure Auto-Scaling**
   - Scale down during off-hours
   - Only pay for capacity you need

4. **Optimize Storage**
   - Set blob lifecycle policies to archive old data
   - Use appropriate access tier (Hot vs Cool)

5. **Application Insights Sampling**
   - Configure adaptive sampling to reduce data ingestion costs
   - Retain only critical telemetry long-term

6. **Static Web App**
   - Free tier is sufficient for development
   - Standard tier only needed for custom domains/advanced features

---

## Future Deployment Options

The following deployment methods are not currently implemented but could be added:

### Docker/Container Deployment

To add Docker support:
1. Create `Dockerfile` in `src/backend` and `src/frontend`
2. Create `docker-compose.yml` in project root
3. Update CI/CD pipeline to build and push images

### Azure Kubernetes Service (AKS)

For high-scale deployments:
1. Create Kubernetes manifests (deployments, services, ingress)
2. Add Terraform modules for AKS cluster
3. Configure Helm charts for application deployment

### Azure Container Instances

For scheduled or sporadic workloads:
1. Add Terraform resources for Container Instances
2. Configure container groups for frontend and backend
3. Set up Azure Container Registry

---

## Additional Resources

- [Azure Static Web Apps Documentation](https://learn.microsoft.com/en-us/azure/static-web-apps/)
- [Azure App Service Documentation](https://learn.microsoft.com/en-us/azure/app-service/)
- [Terraform Azure Provider](https://registry.terraform.io/providers/hashicorp/azurerm/latest/docs)
- [.NET 10 Documentation](https://learn.microsoft.com/en-us/dotnet/)
- [React Documentation](https://react.dev/)
- [Vite Documentation](https://vitejs.dev/)

For additional help:
- Check [README.md](../README.md) for project overview
- See [API.md](./API.md) for API documentation
- Review [ARCHITECTURE.md](./ARCHITECTURE.md) for system design
- Open an issue on GitHub for bugs/feature requests

---

**Version:** 2.0
**Last Updated:** 2026-01-14
**Maintained By:** Development Team

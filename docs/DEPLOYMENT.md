# Deployment Guide

Complete guide for deploying the Blob Copy application to production environments.

## Table of Contents

1. [Prerequisites](#prerequisites)
2. [Deployment Options](#deployment-options)
3. [Local/Development Deployment](#localdevelopment-deployment)
4. [Azure Container Instances](#azure-container-instances)
5. [Azure App Service](#azure-app-service)
6. [Azure Kubernetes Service (AKS)](#azure-kubernetes-service-aks)
7. [Environment Configuration](#environment-configuration)
8. [Database Setup](#database-setup)
9. [Monitoring & Logging](#monitoring--logging)
10. [Troubleshooting](#troubleshooting)
11. [Post-Deployment Checklist](#post-deployment-checklist)

---

## Prerequisites

### Required

- **Azure Subscription** with appropriate permissions
- **Azure Storage Account** with blob containers
- **Docker** (for container deployments)
- **Azure CLI** (`az` command)
- **kubectl** (for Kubernetes deployments)
- **Git** for version control

### Recommended

- **Docker Desktop** for local testing
- **Visual Studio Code** with Azure extensions
- **Azure Storage Explorer** for blob management
- **Application Insights** for monitoring

### Software Versions

```
.NET 10.0+
Node.js 18.0+
Docker 20.10+
Azure CLI 2.40+
kubectl 1.27+
```

---

## Deployment Options

| Option | Complexity | Scalability | Cost | Best For |
|--------|-----------|-------------|------|----------|
| **Local/Dev** | Low | N/A | Free | Development & testing |
| **Docker Compose** | Low | Limited | ~$20/month | Small team testing |
| **Container Instances** | Medium | Manual scaling | ~$50-200/month | Scheduled/sporadic workloads |
| **App Service** | Medium | Auto-scaling | ~$50-500/month | Light-to-medium traffic |
| **Kubernetes (AKS)** | High | Full auto-scaling | ~$200+/month | Production, high availability |

### Recommendation by Use Case

- **POC/Demo**: Docker Compose or Container Instances
- **Small Team**: App Service
- **Enterprise/HA**: Azure Kubernetes Service

---

## Local/Development Deployment

### 1. Clone Repository

```bash
git clone https://github.com/your-org/blob-copy.git
cd blob-copy
```

### 2. Setup Azure Credentials

```bash
# Option A: Azure CLI (DefaultAzureCredential)
az login
az account set --subscription "Your Subscription Name"

# Option B: Storage Account Connection String
# In src/backend/BlobCopy.API/appsettings.Development.json
{
  "AzureStorage": {
    "UseConnectionString": true,
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net"
  }
}
```

### 3. Backend Setup

```bash
cd src/backend

# Restore dependencies
dotnet restore

# Run migrations (if using database)
dotnet ef database update

# Start API server (http://localhost:5000)
dotnet run --project BlobCopy.API
```

### 4. Frontend Setup (separate terminal)

```bash
cd src/frontend

# Install dependencies
npm install

# Configure API URL
export VITE_API_URL=http://localhost:5000
export VITE_SIGNALR_URL=http://localhost:5000/blobcopyhub

# Start dev server (http://localhost:3000)
npm start
```

### 5. Verify Installation

```bash
# Health check
curl http://localhost:5000/health

# Frontend access
open http://localhost:3000
```

---

## Docker Deployment

### 1. Build Docker Images

```bash
# Backend image
docker build -t blob-copy-api:latest src/backend

# Frontend image
docker build -t blob-copy-web:latest src/frontend
```

### 2. Run with Docker Compose

Create `docker-compose.yml` in project root:

```yaml
version: '3.9'

services:
  api:
    image: blob-copy-api:latest
    ports:
      - "5000:5000"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ASPNETCORE_URLS=http://+:5000
      - AzureStorage__UseConnectionString=true
      - AzureStorage__ConnectionString=${AZURE_STORAGE_CONNECTION_STRING}
    healthcheck:
      test: ["CMD", "curl", "-f", "http://localhost:5000/health"]
      interval: 30s
      timeout: 10s
      retries: 3
      start_period: 40s

  web:
    image: blob-copy-web:latest
    ports:
      - "80:80"
    environment:
      - VITE_API_URL=http://api:5000
      - VITE_SIGNALR_URL=http://api:5000/blobcopyhub
    depends_on:
      api:
        condition: service_healthy

volumes:
  app-logs:
```

### 3. Start Services

```bash
# Set environment variables
export AZURE_STORAGE_CONNECTION_STRING="DefaultEndpointsProtocol=https;..."

# Start all services
docker-compose up -d

# View logs
docker-compose logs -f api
docker-compose logs -f web

# Stop services
docker-compose down
```

### 4. Access Application

- Frontend: http://localhost
- API: http://localhost:5000

---

## Azure Container Instances

### 1. Create Resource Group

```bash
az group create \
  --name blob-copy-rg \
  --location eastus
```

### 2. Create Container Registry

```bash
az acr create \
  --resource-group blob-copy-rg \
  --name blobcopyacr \
  --sku Basic
```

### 3. Build and Push Images

```bash
# Login to registry
az acr login --name blobcopyacr

# Build and push backend
az acr build \
  --registry blobcopyacr \
  --image blob-copy-api:latest \
  src/backend

# Build and push frontend
az acr build \
  --registry blobcopyacr \
  --image blob-copy-web:latest \
  src/frontend
```

### 4. Create Container Instances

**Backend:**
```bash
az container create \
  --resource-group blob-copy-rg \
  --name blob-copy-api \
  --image blobcopyacr.azurecr.io/blob-copy-api:latest \
  --cpu 1 \
  --memory 1.5 \
  --ports 5000 \
  --environment-variables \
    ASPNETCORE_ENVIRONMENT=Production \
    ASPNETCORE_URLS="http://+:5000" \
    AzureStorage__UseConnectionString=true \
  --secure-environment-variables \
    AzureStorage__ConnectionString="$AZURE_STORAGE_CONNECTION_STRING" \
  --registry-login-server blobcopyacr.azurecr.io \
  --registry-username <username> \
  --registry-password <password> \
  --dns-name-label blob-copy-api \
  --query ipAddress.fqdn
```

**Frontend:**
```bash
az container create \
  --resource-group blob-copy-rg \
  --name blob-copy-web \
  --image blobcopyacr.azurecr.io/blob-copy-web:latest \
  --cpu 0.5 \
  --memory 0.5 \
  --ports 80 \
  --environment-variables \
    VITE_API_URL="http://blob-copy-api.eastus.azurecontainer.io:5000" \
    VITE_SIGNALR_URL="http://blob-copy-api.eastus.azurecontainer.io:5000/blobcopyhub" \
  --registry-login-server blobcopyacr.azurecr.io \
  --registry-username <username> \
  --registry-password <password> \
  --dns-name-label blob-copy-web
```

### 5. Access Application

```bash
# Get FQDN
az container show \
  --resource-group blob-copy-rg \
  --name blob-copy-web \
  --query ipAddress.fqdn
```

Access at: `http://<fqdn>`

---

## Azure App Service

### 1. Create Resource Group

```bash
az group create \
  --name blob-copy-rg \
  --location eastus
```

### 2. Create App Service Plan

```bash
# Standard plan (auto-scaling capable)
az appservice plan create \
  --name blob-copy-plan \
  --resource-group blob-copy-rg \
  --is-linux \
  --sku S1 \
  --number-of-workers 1
```

### 3. Deploy Backend

```bash
# Create backend app
az webapp create \
  --resource-group blob-copy-rg \
  --plan blob-copy-plan \
  --name blob-copy-api \
  --runtime "DOTNET:10.0"

# Configure startup command
az webapp config set \
  --resource-group blob-copy-rg \
  --name blob-copy-api \
  --startup-file "dotnet BlobCopy.API.dll"

# Set environment variables
az webapp config appsettings set \
  --resource-group blob-copy-rg \
  --name blob-copy-api \
  --settings \
    ASPNETCORE_ENVIRONMENT=Production \
    WEBSITES_ENABLE_APP_SERVICE_STORAGE=false \
    AzureStorage__UseConnectionString=true

# Set connection string (sensitive)
az webapp config connection-string set \
  --resource-group blob-copy-rg \
  --name blob-copy-api \
  --connection-string-type Custom \
  --settings AzureStorage__ConnectionString="$AZURE_STORAGE_CONNECTION_STRING"

# Configure logging
az webapp log config \
  --resource-group blob-copy-rg \
  --name blob-copy-api \
  --application-logging true \
  --detailed-error-messages true \
  --failed-request-tracing true

# Deploy from local
dotnet publish -c Release -o ./publish
cd ./publish
zip -r ../deploy.zip .
az webapp deployment source config-zip \
  --resource-group blob-copy-rg \
  --name blob-copy-api \
  --src ../deploy.zip
```

### 4. Deploy Frontend

```bash
# Create frontend app
az webapp create \
  --resource-group blob-copy-rg \
  --plan blob-copy-plan \
  --name blob-copy-web \
  --runtime "NODE:18-lts"

# Set environment variables
az webapp config appsettings set \
  --resource-group blob-copy-rg \
  --name blob-copy-web \
  --settings \
    VITE_API_URL="https://blob-copy-api.azurewebsites.net" \
    VITE_SIGNALR_URL="https://blob-copy-api.azurewebsites.net/blobcopyhub"

# Build and deploy
cd src/frontend
npm install
npm run build
cd dist
zip -r ../../deploy.zip .
az webapp deployment source config-zip \
  --resource-group blob-copy-rg \
  --name blob-copy-web \
  --src ../../deploy.zip
```

### 5. Configure Custom Domain (Optional)

```bash
# Add custom domain
az webapp config hostname add \
  --resource-group blob-copy-rg \
  --webapp-name blob-copy-web \
  --hostname your-domain.com

# Configure SSL certificate
az webapp config ssl bind \
  --resource-group blob-copy-rg \
  --name blob-copy-web \
  --certificate-thumbprint <thumbprint> \
  --ssl-type SNI
```

### 6. Configure Auto-Scaling

```bash
# Create autoscale settings
az monitor autoscale create \
  --resource-group blob-copy-rg \
  --resource blob-copy-plan \
  --resource-type "Microsoft.Web/serverfarms" \
  --name blob-copy-autoscale \
  --min-count 1 \
  --max-count 5 \
  --count 1

# Add scale-up rule (CPU > 70%)
az monitor autoscale rule create \
  --resource-group blob-copy-rg \
  --autoscale-name blob-copy-autoscale \
  --condition "Percentage CPU > 70 avg 5m" \
  --scale out 1

# Add scale-down rule (CPU < 20%)
az monitor autoscale rule create \
  --resource-group blob-copy-rg \
  --autoscale-name blob-copy-autoscale \
  --condition "Percentage CPU < 20 avg 5m" \
  --scale in 1
```

---

## Azure Kubernetes Service (AKS)

### 1. Create AKS Cluster

```bash
# Create resource group
az group create \
  --name blob-copy-rg \
  --location eastus

# Create AKS cluster
az aks create \
  --resource-group blob-copy-rg \
  --name blob-copy-aks \
  --node-count 3 \
  --vm-set-type VirtualMachineScaleSets \
  --load-balancer-sku standard \
  --enable-managed-identity \
  --network-plugin azure \
  --network-policy azure \
  --docker-bridge-address 172.17.0.1/16 \
  --service-cidr 10.0.0.0/16 \
  --dns-service-ip 10.0.0.10 \
  --generate-ssh-keys

# Get kubeconfig
az aks get-credentials \
  --resource-group blob-copy-rg \
  --name blob-copy-aks \
  --overwrite-existing
```

### 2. Create Container Registry

```bash
az acr create \
  --resource-group blob-copy-rg \
  --name blobcopyacr \
  --sku Standard

# Grant AKS pull access to ACR
az aks update \
  --name blob-copy-aks \
  --resource-group blob-copy-rg \
  --attach-acr blobcopyacr
```

### 3. Build and Push Images

```bash
az acr build \
  --registry blobcopyacr \
  --image blob-copy-api:latest \
  src/backend

az acr build \
  --registry blobcopyacr \
  --image blob-copy-web:latest \
  src/frontend
```

### 4. Create Kubernetes Manifests

**namespace.yaml:**
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: blob-copy
```

**configmap.yaml:**
```yaml
apiVersion: v1
kind: ConfigMap
metadata:
  name: blob-copy-config
  namespace: blob-copy
data:
  ASPNETCORE_ENVIRONMENT: "Production"
  VITE_API_URL: "http://blob-copy-api:5000"
  VITE_SIGNALR_URL: "http://blob-copy-api:5000/blobcopyhub"
```

**secret.yaml:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: blob-copy-secrets
  namespace: blob-copy
type: Opaque
stringData:
  AZURE_STORAGE_CONNECTION_STRING: "DefaultEndpointsProtocol=https;..."
```

**api-deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: blob-copy-api
  namespace: blob-copy
spec:
  replicas: 2
  selector:
    matchLabels:
      app: blob-copy-api
  template:
    metadata:
      labels:
        app: blob-copy-api
    spec:
      containers:
      - name: blob-copy-api
        image: blobcopyacr.azurecr.io/blob-copy-api:latest
        ports:
        - containerPort: 5000
        env:
        - name: ASPNETCORE_URLS
          value: "http://+:5000"
        - name: ASPNETCORE_ENVIRONMENT
          valueFrom:
            configMapKeyRef:
              name: blob-copy-config
              key: ASPNETCORE_ENVIRONMENT
        - name: AzureStorage__UseConnectionString
          value: "true"
        - name: AzureStorage__ConnectionString
          valueFrom:
            secretKeyRef:
              name: blob-copy-secrets
              key: AZURE_STORAGE_CONNECTION_STRING
        livenessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 5000
          initialDelaySeconds: 10
          periodSeconds: 5
        resources:
          requests:
            cpu: "100m"
            memory: "256Mi"
          limits:
            cpu: "500m"
            memory: "512Mi"

---
apiVersion: v1
kind: Service
metadata:
  name: blob-copy-api
  namespace: blob-copy
spec:
  selector:
    app: blob-copy-api
  ports:
  - port: 5000
    targetPort: 5000
  type: ClusterIP
```

**web-deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: blob-copy-web
  namespace: blob-copy
spec:
  replicas: 2
  selector:
    matchLabels:
      app: blob-copy-web
  template:
    metadata:
      labels:
        app: blob-copy-web
    spec:
      containers:
      - name: blob-copy-web
        image: blobcopyacr.azurecr.io/blob-copy-web:latest
        ports:
        - containerPort: 80
        env:
        - name: VITE_API_URL
          valueFrom:
            configMapKeyRef:
              name: blob-copy-config
              key: VITE_API_URL
        - name: VITE_SIGNALR_URL
          valueFrom:
            configMapKeyRef:
              name: blob-copy-config
              key: VITE_SIGNALR_URL
        livenessProbe:
          httpGet:
            path: /
            port: 80
          initialDelaySeconds: 10
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /
            port: 80
          initialDelaySeconds: 5
          periodSeconds: 5
        resources:
          requests:
            cpu: "50m"
            memory: "128Mi"
          limits:
            cpu: "200m"
            memory: "256Mi"

---
apiVersion: v1
kind: Service
metadata:
  name: blob-copy-web
  namespace: blob-copy
spec:
  selector:
    app: blob-copy-web
  ports:
  - port: 80
    targetPort: 80
  type: LoadBalancer
```

**ingress.yaml:**
```yaml
apiVersion: networking.k8s.io/v1
kind: Ingress
metadata:
  name: blob-copy-ingress
  namespace: blob-copy
  annotations:
    cert-manager.io/cluster-issuer: "letsencrypt-prod"
spec:
  ingressClassName: nginx
  tls:
  - hosts:
    - blob-copy.example.com
    secretName: blob-copy-tls
  rules:
  - host: blob-copy.example.com
    http:
      paths:
      - path: /
        pathType: Prefix
        backend:
          service:
            name: blob-copy-web
            port:
              number: 80
      - path: /api
        pathType: Prefix
        backend:
          service:
            name: blob-copy-api
            port:
              number: 5000
```

### 5. Deploy to Kubernetes

```bash
# Create namespace and secrets
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl create secret generic blob-copy-secrets \
  --from-literal=AZURE_STORAGE_CONNECTION_STRING="$AZURE_STORAGE_CONNECTION_STRING" \
  -n blob-copy

# Deploy applications
kubectl apply -f api-deployment.yaml
kubectl apply -f web-deployment.yaml

# Deploy ingress controller
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx \
  --create-namespace

# Deploy ingress
kubectl apply -f ingress.yaml

# Monitor deployment
kubectl get deployments -n blob-copy
kubectl get pods -n blob-copy
kubectl logs -n blob-copy deployment/blob-copy-api
```

### 6. Configure Auto-Scaling

```bash
# Enable metrics server (if not already enabled)
kubectl apply -f https://github.com/kubernetes-sigs/metrics-server/releases/latest/download/components.yaml

# Create HPA for API
kubectl autoscale deployment blob-copy-api \
  --min=2 \
  --max=5 \
  --cpu-percent=70 \
  -n blob-copy

# Create HPA for Web
kubectl autoscale deployment blob-copy-web \
  --min=2 \
  --max=5 \
  --cpu-percent=80 \
  -n blob-copy
```

---

## Environment Configuration

### Backend (appsettings.json)

**Development:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug"
    }
  },
  "AzureStorage": {
    "UseConnectionString": true,
    "ConnectionString": "UseDevelopmentStorage=true"
  },
  "SignalR": {
    "MaxMessageSize": 1048576
  }
}
```

**Production:**
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning"
    },
    "ApplicationInsights": {
      "InstrumentationKey": "YOUR_APP_INSIGHTS_KEY"
    }
  },
  "AzureStorage": {
    "UseConnectionString": true,
    "ConnectionString": "${AZURE_STORAGE_CONNECTION_STRING}"
  },
  "SignalR": {
    "MaxMessageSize": 104857600,
    "ClientTimeoutInterval": 60000
  },
  "Cors": {
    "AllowedOrigins": [
      "https://your-domain.com",
      "https://app.your-domain.com"
    ]
  }
}
```

### Frontend (.env files)

**Development (.env.development):**
```
VITE_API_URL=http://localhost:5000
VITE_SIGNALR_URL=http://localhost:5000/blobcopyhub
VITE_LOG_LEVEL=debug
```

**Production (.env.production):**
```
VITE_API_URL=https://api.your-domain.com
VITE_SIGNALR_URL=https://api.your-domain.com/blobcopyhub
VITE_LOG_LEVEL=error
```

---

## Database Setup

If using persistent storage for operations history:

### 1. Create Storage Account

```bash
az storage account create \
  --resource-group blob-copy-rg \
  --name blobcopystorage \
  --sku Standard_GRS \
  --kind StorageV2
```

### 2. Create Database (Optional - SQL Database)

```bash
# Create SQL server
az sql server create \
  --name blob-copy-server \
  --resource-group blob-copy-rg \
  --admin-user adminuser \
  --admin-password 'YourComplexPassword123!'

# Create database
az sql db create \
  --server blob-copy-server \
  --name blob-copy-db \
  --edition Standard \
  --service-objective S1

# Run Entity Framework migrations
dotnet ef database update --connection "Server=blob-copy-server.database.windows.net;Database=blob-copy-db;User Id=adminuser;Password='YourComplexPassword123!';"
```

---

## Monitoring & Logging

### 1. Enable Application Insights

```bash
# Create Application Insights
az monitor app-insights component create \
  --app blob-copy \
  --location eastus \
  --resource-group blob-copy-rg

# Get instrumentation key
az monitor app-insights component show \
  --app blob-copy \
  --resource-group blob-copy-rg \
  --query instrumentationKey
```

### 2. Configure in Backend

Add to `appsettings.json`:
```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key"
  }
}
```

### 3. View Logs and Metrics

```bash
# App Service logs
az webapp log tail --resource-group blob-copy-rg --name blob-copy-api

# Application Insights queries
az monitor metrics list \
  --resource /subscriptions/{id}/resourceGroups/blob-copy-rg/providers/microsoft.insights/components/blob-copy \
  --metric HttpRequestsPerSecond
```

### 4. Create Alerts

```bash
az monitor metrics alert create \
  --name blob-copy-errors \
  --resource-group blob-copy-rg \
  --scopes "/subscriptions/{id}/resourceGroups/blob-copy-rg/providers/microsoft.insights/components/blob-copy" \
  --condition "avg ServerResponseTime > 5000" \
  --description "Alert when response time exceeds 5 seconds" \
  --evaluation-frequency 1m \
  --window-size 5m
```

---

## Troubleshooting

### Common Issues

**1. Azure Credentials Not Found**
```
Error: "Could not automatically determine credentials"
```
Solution:
```bash
az login
az account set --subscription "Your Subscription"
# Or set connection string in appsettings
```

**2. CORS Errors**
```
"Access-Control-Allow-Origin" header not present
```
Solution:
- Update CORS settings in backend
- Ensure frontend URL is in allowed origins

**3. WebSocket Connection Fails**
```
WebSocket connection to failed
```
Solution:
- Verify SignalR hub URL is correct
- Check firewall allows WebSocket (port 443)
- Enable CORS for WebSocket

**4. Out of Memory**
```
OutOfMemoryException during copy
```
Solution:
- Increase container memory limits
- Check chunk size configuration (default 1MB)
- Monitor with Application Insights

**5. Slow Performance**
```
Copy transfer rate < expected
```
Solution:
- Verify network bandwidth (100+ Mbps recommended)
- Check VM/container CPU usage
- Scale horizontally (more replicas)
- Enable caching where applicable

### Debug Commands

```bash
# Check API health
curl https://your-api-domain/health

# Check logs (App Service)
az webapp log tail --resource-group blob-copy-rg --name blob-copy-api --follow

# Check logs (AKS)
kubectl logs deployment/blob-copy-api -n blob-copy

# Test Azure Storage connectivity
az storage blob list --account-name youraccountname --container-name yourcontainer

# Check certificate validity
openssl s_client -connect your-domain.com:443
```

---

## Post-Deployment Checklist

### Before Going Live

- [ ] Run health check: `GET /health` returns 200 OK
- [ ] Frontend loads without errors
- [ ] Form validation works
- [ ] Test copy operation end-to-end
- [ ] Test cancellation functionality
- [ ] Verify error handling shows appropriate messages
- [ ] Check performance metrics (< 5s response time)
- [ ] Verify HTTPS/SSL certificate is valid
- [ ] Setup monitoring and alerting
- [ ] Create backup strategy
- [ ] Document custom configurations
- [ ] Set up runbook for common issues
- [ ] Train support team on troubleshooting

### Security Checklist

- [ ] Azure credentials are not in code/logs
- [ ] CORS is restricted to known domains
- [ ] HTTPS/TLS is enforced
- [ ] Azure Storage keys rotated regularly
- [ ] SQL passwords meet complexity requirements
- [ ] API rate limiting is configured
- [ ] Input validation is enabled on all endpoints
- [ ] Authentication/Authorization is configured
- [ ] Audit logging is enabled
- [ ] DDoS protection is configured (Azure DDoS Standard)

### Operational Checklist

- [ ] Auto-scaling is configured
- [ ] Backup strategy is implemented
- [ ] Disaster recovery plan exists
- [ ] Monitoring dashboards are created
- [ ] Alert thresholds are reasonable
- [ ] On-call rotation is established
- [ ] Runbooks are documented
- [ ] Team is trained on operations
- [ ] Deployment procedure is documented
- [ ] Rollback procedure is tested

### Performance Checklist

- [ ] API response time < 5 seconds
- [ ] Database query performance is acceptable
- [ ] WebSocket latency < 100ms
- [ ] CPU usage stays below 70%
- [ ] Memory usage is within limits
- [ ] Disk I/O is reasonable
- [ ] Network bandwidth is sufficient

---

## Maintenance

### Regular Tasks

**Daily:**
- Monitor error rates in Application Insights
- Check auto-scaling activity
- Verify copy operations completing successfully

**Weekly:**
- Review performance metrics
- Check for security updates
- Test backup restoration procedure

**Monthly:**
- Update dependencies (security patches)
- Review and optimize costs
- Analyze capacity planning needs
- Test disaster recovery

**Quarterly:**
- Major version upgrades
- Security audit
- Capacity planning review
- Cost optimization review

### Scaling Strategy

**Vertical Scaling (increase resources):**
- Increase VM size (App Service, AKS nodes)
- Increase CPU/memory limits
- Use for performance-critical components

**Horizontal Scaling (add replicas):**
- Add more pod/container replicas
- Distribute load across instances
- Use for high-traffic workloads

**Recommendations:**
- Start with 2-3 replicas for high availability
- Scale API tier more aggressively than web tier
- Monitor actual usage patterns
- Plan for 2-3x peak capacity

---

## Cost Optimization

### Cost Reduction Strategies

1. **Right-sizing Instances**
   - Monitor actual CPU/memory usage
   - Downsize over-provisioned resources
   - Use spot instances where applicable

2. **Reserved Instances**
   - Commit to 1-3 year terms
   - Save 30-60% vs. on-demand

3. **Auto-scaling Policies**
   - Scale down during off-peak hours
   - Use scheduled scaling for predictable patterns

4. **Storage Optimization**
   - Use tiered storage classes
   - Archive old operation records

5. **Network Optimization**
   - Use content delivery network (CDN) for static assets
   - Minimize data transfer between regions

### Estimated Monthly Costs

**Small Deployment (App Service):**
- App Service Plan (S1): $50
- Azure Storage: $20
- Application Insights: $0 (free tier)
- **Total: ~$70/month**

**Medium Deployment (AKS):**
- AKS Cluster (3 nodes): $200
- Container Registry: $100
- Azure Storage: $30
- Application Insights: $50
- **Total: ~$380/month**

**Large Deployment (Multi-region AKS):**
- Primary AKS Cluster: $200
- Secondary AKS Cluster: $200
- Azure SQL Database: $200
- Container Registry: $100
- Storage: $50
- Monitoring: $100
- **Total: ~$850/month**

---

## Support & Documentation

For additional help:
- Check [README.md](../README.md) for general setup
- See [API.md](./API.md) for API documentation
- Review [ARCHITECTURE.md](./ARCHITECTURE.md) for system design
- Open an issue on GitHub for bugs/feature requests

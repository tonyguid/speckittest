# Quickstart: Blob Copy Feature Development

**Updated**: January 4, 2026
**Version**: 1.0

---

## Prerequisites

### Required
- **Node.js**: 18.0+ (for frontend)
- **npm**: 9.0+ (or yarn/pnpm)
- **.NET SDK**: 10.0 (for backend)
- **Git**: 2.30+
- **Azure CLI**: 2.50+ (for Azure Storage connection)

### Optional
- **Visual Studio Code**: Latest with C# Dev Kit extension
- **Docker Desktop**: For containerized local testing
- **Azure Storage Explorer**: For visual blob inspection

### Azure Subscription
- Azure Storage Account (create one or use existing)
- Azure Entra ID Application Registration (for service principal auth in dev)

---

## 1. Environment Setup

### 1.1 Clone Repository

```bash
git clone https://github.com/yourusername/blob-copy-feature.git
cd blob-copy-feature
git checkout 001-blob-copy
```

### 1.2 Install Dependencies

#### Backend (.NET)
```bash
cd src/backend/BlobCopy.API
dotnet restore
```

#### Frontend (Node.js)
```bash
cd src/frontend
npm install
```

#### Install Global Tools (Optional)
```bash
# Azure CLI for managing storage accounts
curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash

# Azure Storage Explorer
# Download from: https://azure.microsoft.com/products/storage/storage-explorer/
```

---

## 2. Configure Credentials

### 2.1 Local Development with Azure Emulator (Recommended)

#### Option A: Azure Storage Emulator (Windows)
```bash
# Download and install
# https://go.microsoft.com/fwlink/?linkid=717179

# Start emulator
AzureStorageEmulator.exe start

# Or use Azurite (cross-platform, Docker)
npm install -g azurite
azurite --silent --location c:\azurite --debug c:\azurite\debug.log
```

#### Option B: Azurite (Recommended for all platforms)
```bash
# Install globally
npm install -g azurite

# Run in terminal
azurite

# Or run in Docker
docker run -d -p 10000:10000 -p 10001:10001 mcr.microsoft.com/azure-storage/azurite
```

The emulator runs at: `http://127.0.0.1:10000`

---

### 2.2 Local Development with Real Azure Storage

#### Step 1: Create Storage Account
```bash
# Login to Azure
az login

# Create resource group
az group create \
  --name blob-copy-dev \
  --location eastus

# Create storage account
az storage account create \
  --name blobcopydevXXXX \
  --resource-group blob-copy-dev \
  --location eastus \
  --sku Standard_LRS
```

#### Step 2: Get Connection String
```bash
az storage account show-connection-string \
  --name blobcopydevXXXX \
  --resource-group blob-copy-dev \
  --query connectionString \
  -o tsv

# Copy the connection string (looks like):
# DefaultEndpointsProtocol=https;AccountName=...;AccountKey=...;EndpointSuffix=core.windows.net
```

#### Step 3: Create Containers
```bash
az storage container create \
  --account-name blobcopydevXXXX \
  --name source-blobs

az storage container create \
  --account-name blobcopydevXXXX \
  --name dest-blobs
```

---

### 2.3 Configuration Files

#### Backend: `src/backend/BlobCopy.API/appsettings.Development.json`
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Information"
    }
  },
  "Azure": {
    "Storage": {
      "ConnectionString": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=...;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;",
      "ServiceUrl": "http://127.0.0.1:10000"
    },
    "Authentication": {
      "TenantId": "YOUR_TENANT_ID",
      "ClientId": "YOUR_APP_ID",
      "ClientSecret": "YOUR_CLIENT_SECRET"
    }
  },
  "ApplicationInsights": {
    "InstrumentationKey": "YOUR_INSTRUMENTATION_KEY"
  },
  "Timeouts": {
    "CopyTimeoutMinutes": 30,
    "DefaultTimeoutSeconds": 300
  }
}
```

#### Frontend: `src/frontend/.env.local`
```bash
VITE_API_BASE_URL=http://localhost:5000
VITE_SIGNALR_HUB_URL=ws://localhost:5000/blobcopyhub
```

---

## 3. Run Backend

### 3.1 Using dotnet CLI

```bash
cd src/backend/BlobCopy.API
dotnet run
```

Expected output:
```
info: Microsoft.Hosting.Lifetime[14]
      Now listening on: http://localhost:5000
info: Microsoft.Hosting.Lifetime[0]
      Application started. Press Ctrl+C to stop.
```

### 3.2 Using Visual Studio Code

1. Open `src/backend/BlobCopy.API` folder in VS Code
2. Install "C# Dev Kit" extension
3. Press `F5` or go to Run → Start Debugging

### 3.3 Health Check

```bash
curl http://localhost:5000/api/v1/health

# Expected response:
# {
#   "status": "healthy",
#   "timestamp": "2026-01-04T14:30:00Z"
# }
```

---

## 4. Run Frontend

### 4.1 Development Server

```bash
cd src/frontend
npm run dev
```

Expected output:
```
  VITE v5.x.x  ready in xxx ms

  ➜  Local:   http://localhost:3000/
  ➜  Network: use --host to expose
```

### 4.2 Build for Production

```bash
npm run build

# Creates optimized build in ./dist/
ls -la dist/
```

---

## 5. Test the Feature Locally

### 5.1 Manual Testing

1. **Open the app**: http://localhost:3000

2. **Create test blobs** (using Azure Storage Explorer or CLI):
   ```bash
   # Using Azure CLI
   echo "This is test data" > testfile.txt
   
   az storage blob upload \
     --account-name blobcopydevXXXX \
     --container-name source-blobs \
     --name testblob.txt \
     --file testfile.txt
   ```

3. **Test the UI**:
   - Enter source URI: `https://blobcopydevXXXX.blob.core.windows.net/source-blobs/testblob.txt`
   - Enter destination URI: `https://blobcopydevXXXX.blob.core.windows.net/dest-blobs/testblob-copy.txt`
   - Click "Copy"
   - Watch progress bar update
   - See success message

   **Test identical URI validation**:
   - Enter the same URI in both source and destination fields: `https://blobcopydevXXXX.blob.core.windows.net/source-blobs/testblob.txt`
   - Expected: Error message displays "Source and destination URIs cannot be identical"
   - Expected: Copy button is disabled or form prevents submission
   - Test case-insensitive: Try `https://BLOBCOPYDEVXXXX.blob.core.windows.net/SOURCE-BLOBS/testblob.txt` in destination
   - Expected: Same error appears (validation is case-insensitive)

4. **Verify destination blob**:
   ```bash
   az storage blob show \
     --account-name blobcopydevXXXX \
     --container-name dest-blobs \
     --name testblob-copy.txt
   ```

---

### 5.2 Running Unit Tests

#### Backend (xUnit)
```bash
cd src/backend/BlobCopy.Tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverageFormat=opencover
```

Expected output:
```
Passed!  - Failed: 0, Passed: 42, Skipped: 0
```

#### Frontend (Vitest)
```bash
cd src/frontend
npm test

# Watch mode
npm run test:watch

# Coverage
npm run test:coverage
```

---

### 5.3 Running E2E Tests (Playwright)

```bash
cd src/frontend
npm run test:e2e

# Or specific test file
npx playwright test e2e/blob-copy.spec.ts

# Debug mode
npx playwright test --debug

# UI mode (interactive)
npx playwright test --ui
```

---

## 6. Project Structure

```
my-project/
├── src/
│   ├── backend/                      # C# .NET 10 API
│   │   ├── BlobCopy.API/
│   │   │   ├── Controllers/
│   │   │   │   └── BlobCopyController.cs
│   │   │   ├── Services/
│   │   │   │   ├── BlobCopyService.cs
│   │   │   │   ├── BlobValidationService.cs
│   │   │   │   ├── ProgressNotificationService.cs
│   │   │   │   └── IBlobClientFactory.cs
│   │   │   ├── Models/
│   │   │   │   ├── BlobCopyOperation.cs
│   │   │   │   ├── BlobCopyStatus.cs
│   │   │   │   ├── CopyRequest.cs
│   │   │   │   ├── ValidationError.cs
│   │   │   │   └── ConflictResponse.cs
│   │   │   ├── Hubs/
│   │   │   │   └── BlobCopyHub.cs
│   │   │   ├── Middleware/
│   │   │   │   └── ErrorHandlingMiddleware.cs
│   │   │   ├── appsettings.json
│   │   │   └── Program.cs
│   │   └── BlobCopy.Tests/
│   │       ├── Unit/
│   │       │   ├── Services/
│   │       │   │   ├── BlobCopyServiceTests.cs
│   │       │   │   └── BlobValidationServiceTests.cs
│   │       │   ├── Controllers/
│   │       │   │   └── BlobCopyControllerTests.cs
│   │       │   └── ApiContractTests.cs
│   │       └── Integration/
│   │           └── Controllers/
│   │
│   └── frontend/                     # React 18+ SPA (Vite)
│       ├── src/
│       │   ├── components/
│       │   │   ├── CopyForm.tsx
│       │   │   ├── ProgressDisplay.tsx
│       │   │   ├── ResultDisplay.tsx
│       │   │   └── OverwriteConfirmDialog.tsx
│       │   ├── hooks/
│       │   │   ├── useCopyOperation.ts
│       │   │   └── useProgress.ts
│       │   ├── services/
│       │   │   ├── apiClient.ts
│       │   │   ├── signalRClient.ts
│       │   │   ├── uriValidationService.ts
│       │   │   └── applicationInsightsService.ts
│       │   ├── types.ts
│       │   ├── App.tsx
│       │   └── main.tsx
│       ├── src/__tests__/
│       │   ├── components/
│       │   │   ├── CopyForm.test.tsx
│       │   │   ├── ProgressDisplay.test.tsx
│       │   │   └── OverwriteConfirmDialog.test.tsx
│       │   ├── hooks/
│       │   │   └── useCopyOperation.test.ts
│       │   └── services/
│       │       ├── apiClient.test.ts
│       │       └── uriValidationService.test.ts
│       ├── e2e/
│       │   ├── blob-copy.spec.ts
│       │   └── blob-copy-conflict.spec.ts
│       ├── package.json
│       ├── vite.config.ts
│       ├── tsconfig.json
│       └── playwright.config.ts
│
├── specs/
│   └── 001-blob-copy/
│       ├── spec.md                   # Feature specification
│       ├── plan.md                   # Implementation plan
│       ├── data-model.md             # Entity definitions
│       ├── quickstart.md             # This file
│       ├── contracts/
│       │   ├── api.openapi.yaml      # OpenAPI spec
│       │   ├── api.contracts.md      # API documentation
│       │   └── signalr.events.md     # WebSocket events
│       └── tasks.md                  # Development task breakdown
│
├── infra/
│   └── terraform/                    # Infrastructure as Code
│       └── main.tf
│
├── .specify/
│   ├── memory/
│   │   └── constitution.md           # Project governance
│   └── templates/
│
├── .github/
│   └── workflows/
│       └── ci.yml                    # CI/CD workflow
│
├── .gitignore
└── README.md
```

---

## 7. Common Tasks

### 7.1 Create Test Blobs

```bash
# Using Azure CLI
dd if=/dev/urandom bs=1M count=100 of=large-test-blob.bin
az storage blob upload \
  --account-name blobcopydevXXXX \
  --container-name source-blobs \
  --name large-blob.bin \
  --file large-test-blob.bin
```

### 7.2 Debug Backend

#### Visual Studio Code
1. Open `src/backend/BlobCopy.API` folder
2. Press `F5` to start debugging
3. Set breakpoints (click left margin)
4. Make API request to hit breakpoint

#### Command Line
```bash
cd src/backend/BlobCopy.API
dotnet run --configuration Debug
```

### 7.3 Debug Frontend

#### Chrome DevTools
1. Open http://localhost:3000
2. Press `F12` to open DevTools
3. Set breakpoints in Sources tab

#### VS Code Debugger
```bash
# In VS Code, open debug terminal:
# Select "JavaScript Debug Terminal"
cd src/frontend
npm run dev
```

### 7.4 Check Test Coverage

```bash
# Backend coverage
cd src/backend/BlobCopy.Tests
dotnet test /p:CollectCoverage=true /p:CoverageFormat=lcov

# Frontend coverage
cd src/frontend
npm run test:coverage
```

---

## 8. Troubleshooting

### Problem: "Failed to connect to Azure Storage"

**Solution**:
1. Verify Azurite is running: `curl http://127.0.0.1:10000`
2. Check `appsettings.Development.json` connection string
3. Ensure port 10000 is not blocked by firewall

### Problem: "CORS error when calling API"

**Solution**:
1. Verify CORS enabled in `Program.cs`:
   ```csharp
   app.UseCors(policy => policy
     .AllowAnyOrigin()
     .AllowAnyMethod()
     .AllowAnyHeader());
   ```
2. Check `REACT_APP_API_BASE_URL` matches backend URL

### Problem: "Playwright tests fail with timeout"

**Solution**:
1. Increase timeout in `playwright.config.ts`:
   ```typescript
   timeout: 30 * 1000,
   ```
2. Run in debug mode: `npx playwright test --debug`

### Problem: ".NET version mismatch"

**Solution**:
```bash
dotnet --version  # Check installed version
dotnet sdk list   # List all installed SDKs
dotnet new global.json  # Create global.json for .NET 10
```

---

## 9. Environment Variables Reference

| Variable | Backend | Frontend | Description |
|----------|---------|----------|-------------|
| `ASPNETCORE_URLS` | Yes | | Backend port (default: http://localhost:5000) |
| `ASPNETCORE_ENVIRONMENT` | Yes | | Set to `Development` for local dev |
| `Azure__Storage__ConnectionString` | Yes | | Azure Storage connection string |
| `Azure__Authentication__TenantId` | Yes | | Entra ID tenant ID |
| `Azure__Authentication__ClientId` | Yes | | Application ID |
| `VITE_API_BASE_URL` | | Yes | Backend API base URL |
| `VITE_SIGNALR_HUB_URL` | | Yes | SignalR hub WebSocket URL |

---

## 10. Next Steps

1. ✅ Complete local setup above
2. 🔄 Run `/speckit.tasks` to get development task breakdown
3. 🔄 Follow task priority to implement features
4. 🔄 Maintain ≥80% unit test coverage
5. 🔄 Submit PRs with Copilot review enabled
6. 📋 Review [Implementation Plan](./plan.md) for technical decisions

---

## Support

- **Issues**: Create GitHub issue with `[blob-copy]` prefix
- **Slack**: `#blob-copy-development`
- **Docs**: See [specs/001-blob-copy/](../)
- **Constitution**: See [.specify/memory/speckit.constitution](../../../.specify/memory/speckit.constitution)


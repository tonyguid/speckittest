# Blob Copy Application

A high-performance Azure Blob Storage copy utility with real-time progress tracking via WebSocket.

## Overview

This application provides a web interface for copying large blobs between Azure Storage containers with:

- **Real-time Progress Tracking**: WebSocket-based progress updates via SignalR
- **Concurrent Operations**: Handle multiple simultaneous blob copies
- **Error Handling**: Comprehensive validation and error reporting
- **Cancellation Support**: Stop in-progress copy operations
- **Cross-browser Support**: Chrome, Firefox, Safari
- **REST + WebSocket Architecture**: Scalable, event-driven design

## Tech Stack

### Backend
- **.NET 10**: Modern C# framework
- **Azure Blob Storage SDK**: Official Azure storage integration
- **ASP.NET Core SignalR**: Real-time WebSocket communication
- **xUnit + Moq**: Comprehensive testing framework

### Frontend
- **React 18+**: UI framework with hooks
- **TypeScript**: Type-safe development
- **Axios**: HTTP client for REST API
- **@microsoft/signalr**: SignalR client for WebSocket
- **Vitest + React Testing Library**: Testing framework
- **Playwright**: E2E testing

## Quick Start

### Prerequisites

- **.NET 10 SDK** (https://dotnet.microsoft.com/download/dotnet/10.0)
- **Node.js 18+** (https://nodejs.org/)
- **Azure Storage Account** with blob containers
- **Azure Credentials** (DefaultAzureCredential or connection string)

### Backend Setup

```bash
cd src/backend

# Install dependencies (implicit in .NET)
dotnet restore

# Build
dotnet build

# Run tests
dotnet test

# Start API server (runs on http://localhost:5000)
dotnet run --project BlobCopy.API
```

### Frontend Setup

```bash
cd src/frontend

# Install dependencies
npm install

# Run tests
npm test

# Start dev server (runs on http://localhost:3000)
npm start

# Build for production
npm run build
```

### Environment Configuration

#### Backend (src/backend/BlobCopy.API/appsettings.json)

```json
{
  "AzureStorage": {
    "UseConnectionString": true,
    "ConnectionString": "DefaultEndpointsProtocol=https;AccountName=myaccount;AccountKey=...;EndpointSuffix=core.windows.net"
  },
  "SignalR": {
    "MaxMessageSize": 1048576
  }
}
```

#### Frontend (.env)

```
VITE_API_URL=http://localhost:5000
VITE_SIGNALR_URL=http://localhost:5000/blobcopyhub
```

## API Documentation

See [API.md](./docs/API.md) for comprehensive REST API documentation.

### Quick API Reference

#### Validate Endpoint
```http
POST /api/blobcopy/validate
Content-Type: application/json

{
  "sourceUri": "https://account.blob.core.windows.net/source/blob",
  "destinationUri": "https://account.blob.core.windows.net/dest/blob"
}
```

#### Start Copy
```http
POST /api/blobcopy/start
Content-Type: application/json

{
  "sourceUri": "https://account.blob.core.windows.net/source/blob",
  "destinationUri": "https://account.blob.core.windows.net/dest/blob"
}
```

#### Get Status
```http
GET /api/blobcopy/status/{operationId}
```

#### Cancel Copy
```http
POST /api/blobcopy/cancel/{operationId}
```

#### Health Check
```http
GET /health
```

## Architecture

See [ARCHITECTURE.md](./docs/ARCHITECTURE.md) for detailed architecture documentation.

### High-Level Overview

```
┌─────────────┐
│   Browser   │
└──────┬──────┘
       │
       ├─────────── REST API ──────────┐
       │                               │
       └────── WebSocket (SignalR) ────┤
                                       │
                               ┌───────▼──────────┐
                               │  .NET 10 API     │
                               ├──────────────────┤
                               │ • Validation     │
                               │ • Copy Service   │
                               │ • Progress Hub   │
                               └───────┬──────────┘
                                       │
                               ┌───────▼──────────┐
                               │ Azure Blob       │
                               │ Storage          │
                               └──────────────────┘
```

## Testing

### Unit Tests

```bash
# Backend
cd src/backend
dotnet test

# Frontend
cd src/frontend
npm test
```

### Integration Tests

```bash
# Backend (runs with unit tests)
cd src/backend
dotnet test
```

### E2E Tests

```bash
cd src/frontend

# Install Playwright browsers
npx playwright install

# Run E2E tests
npm run test:e2e

# Run E2E tests with UI
npm run test:e2e -- --ui
```

### Coverage Reports

```bash
# Backend
cd src/backend
dotnet test /p:CollectCoverage=true

# Frontend
cd src/frontend
npm run test:coverage
```

## CI/CD Pipeline

GitHub Actions automatically runs on every push to `main` and `develop` branches:

1. **Backend Tests**: xUnit test suite
2. **Frontend Tests**: Vitest unit tests
3. **E2E Tests**: Playwright across Chrome, Firefox, Safari
4. **Code Quality**: ESLint, code formatting, .NET analysis
5. **Docker Build**: Create container images (main branch only)

See [.github/workflows/ci.yml](./.github/workflows/ci.yml) for full pipeline definition.

## Development Workflow

### Adding a Feature

1. Create a feature branch: `git checkout -b feature/my-feature`
2. Implement feature with tests
3. Run tests locally: `npm test` (frontend) or `dotnet test` (backend)
4. Push and open a Pull Request
5. GitHub Actions validates all checks pass
6. Merge to `develop` after review
7. Merge to `main` when ready to deploy

### Project Structure

```
my-project/
├── src/
│   ├── backend/
│   │   ├── BlobCopy.API/           # .NET API
│   │   │   ├── Controllers/
│   │   │   ├── Services/
│   │   │   ├── Models/
│   │   │   └── Hubs/
│   │   └── BlobCopy.Tests/         # Unit & integration tests
│   └── frontend/
│       ├── src/
│       │   ├── components/         # React components
│       │   ├── hooks/              # Custom React hooks
│       │   ├── services/           # API & SignalR clients
│       │   └── types/              # TypeScript types
│       ├── e2e/                    # Playwright tests
│       ├── __tests__/              # Unit tests
│       └── package.json
├── docs/
│   ├── API.md                      # API documentation
│   ├── ARCHITECTURE.md             # Architecture guide
│   └── DEPLOYMENT.md               # Deployment guide
└── .github/
    └── workflows/
        └── ci.yml                  # GitHub Actions pipeline
```

## Troubleshooting

### Backend Issues

**Error: "Invalid Azure credentials"**
- Ensure Azure credentials are configured
- Use `az login` for DefaultAzureCredential
- Or set connection string in appsettings.json

**Error: "Blob not found"**
- Verify source blob URI is correct
- Ensure authenticated user has read access to source container

### Frontend Issues

**Error: "Cannot connect to API"**
- Verify backend is running on http://localhost:5000
- Check VITE_API_URL environment variable
- Verify CORS is enabled in backend

**WebSocket connection fails**
- Check SignalR hub URL matches backend configuration
- Verify firewall/proxy allows WebSocket connections
- Check browser console for connection errors

### E2E Test Issues

**Tests timeout on localhost**
- Increase timeout in playwright.config.ts
- Ensure both frontend and backend are running
- Check services are responding: `curl http://localhost:3000` and `curl http://localhost:5000/health`

## Performance Considerations

- **Chunked Copying**: Large blobs are copied in chunks to avoid memory pressure
- **WebSocket Updates**: Real-time progress via SignalR reduces polling overhead
- **Connection Pooling**: Azure SDK manages connection pooling automatically
- **Concurrent Operations**: Multiple copies can run simultaneously

## Security

- **Azure Credentials**: Uses DefaultAzureCredential (respects Azure CLI, Managed Identity, etc.)
- **CORS**: Configured to allow frontend origin only
- **Input Validation**: Both client and server validate blob URIs
- **Error Messages**: Server errors don't leak sensitive information

## Contributing

Contributions welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass: `npm test && dotnet test`
5. Submit a Pull Request

## License

MIT License - See LICENSE file for details

## Support

For issues and questions:
- Open an GitHub Issue
- Check existing issues for similar problems
- Review documentation in `/docs` folder

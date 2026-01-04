# Architecture Guide

## Overview

The Blob Copy application follows a modern **REST + WebSocket** architecture with clear separation between frontend and backend, leveraging Azure services for scalability.

```
┌──────────────────────────────────────────────────────────────┐
│                     Browser (React)                          │
│  ┌─────────────────────────────────────────────────────────┐ │
│  │ • CopyForm (input, validation)                          │ │
│  │ • ProgressDisplay (real-time metrics)                   │ │
│  │ • ResultDisplay (success/failure)                       │ │
│  └─────────────────────────────────────────────────────────┘ │
└──────────────────────────────────────────────────────────────┘
  │                                                   │
  ├─── REST API (Axios) ─────────┐                  │
  │                               │                  │
  └──── WebSocket (SignalR) ──────┼──────────────────┤
                                   │                  │
                    ┌──────────────▼──────────────┐  │
                    │   .NET 10 API Server        │  │
                    │   (ASP.NET Core)            │  │
                    ├─────────────────────────────┤  │
                    │ • BlobCopyController        │  │
                    │   - Validate endpoint       │  │
                    │   - Start endpoint          │  │
                    │   - Status endpoint         │  │
                    │   - Cancel endpoint         │  │
                    │   - Health endpoint         │  │
                    │                             │  │
                    │ • BlobCopyHub (SignalR)     │  │
                    │   - ProgressUpdate          │  │
                    │   - CopyCompleted           │  │
                    │   - CopyFailed              │  │
                    │   - OperationCancelled      │  │
                    └─────────────┬───────────────┘  │
                                  │                  │
                    ┌─────────────┴────────────────┐ │
                    │   Services Layer             │ │
                    ├──────────────────────────────┤ │
                    │ • BlobValidationService      │ │
                    │   - Validate blob URIs       │ │
                    │   - Check existence          │ │
                    │   - Verify permissions       │ │
                    │                              │ │
                    │ • BlobCopyService            │ │
                    │   - Chunked streaming copy   │ │
                    │   - Progress tracking        │ │
                    │   - Cancellation support     │ │
                    │                              │ │
                    │ • ProgressNotificationSvc    │ │
                    │   - SignalR broadcasts       │ │
                    │   - Group management         │ │
                    └──────────────┬───────────────┘ │
                                   │                 │
                    ┌──────────────┴────────────────┐│
                    │ Azure Blob Storage SDK       ││
                    ├──────────────────────────────┤│
                    │ • BlobClient (source)         ││
                    │ • BlobClient (destination)    ││
                    │ • ContainerClient             ││
                    └──────────────┬────────────────┘│
                                   │                 │
                    ┌──────────────┴────────────────┐│
                    │   Azure Blob Storage         ││
                    │   (Cloud Service)            ││
                    └──────────────────────────────┘│
```

---

## Core Components

### Frontend Architecture

#### Component Hierarchy

```
<App>
  ├─ <CopyForm>
  │  └─ useCopyOperation hook
  │     └─ useProgress hook
  ├─ <ProgressDisplay>
  │  └─ useProgress hook
  └─ <ResultDisplay>
```

#### Services

**ApiClient (Axios-based HTTP)**
- Methods: `validate()`, `startCopy()`, `getStatus()`, `cancelCopy()`, `checkHealth()`
- Error handling: Normalizes 400/404 responses, provides context messages
- Singleton instance for application-wide use
- 30-second request timeout

**SignalRClient (HubConnection-based WebSocket)**
- Methods: `connect()`, `disconnect()`, `subscribeToOperation()`, `unsubscribeFromOperation()`
- Auto-reconnection: 5 attempts with exponential backoff (0, 3, 5, 10, 15, 30 seconds)
- Event handlers: 6 server-push events (ProgressUpdate, CopyCompleted, CopyFailed, etc.)
- Callback registration for decoupled event handling

#### Custom Hooks

**useCopyOperation**
- State: `operation`, `isLoading`, `error`
- Methods: `startOperation()`, `cancelOperation()`, `reset()`
- Features:
  - Integrates ApiClient + SignalRClient
  - Fallback to polling if WebSocket unavailable
  - Automatic cleanup on unmount
  - Progress updates via SignalR or polling (1s interval)

**useProgress**
- Computes metrics from operation state
- Returns: percentage, transfer rate, ETA, byte counts
- Memoized calculations for performance
- Time-based ETA with formatted label (MM:SS)

---

### Backend Architecture

#### API Controller (BlobCopyController)

```csharp
public class BlobCopyController : ControllerBase
{
  // POST /api/blobcopy/validate
  // Validates blobs without starting copy
  
  // POST /api/blobcopy/start
  // Initiates copy operation (returns 201 Created)
  // Starts background task for actual copying
  
  // GET /api/blobcopy/status/{id}
  // Returns current operation status
  
  // POST /api/blobcopy/cancel/{id}
  // Cancels in-progress operation
  
  // GET /health
  // Service health check
}
```

#### Service Layer

**BlobValidationService**
- Validates blob URIs format and accessibility
- Checks blob existence via `GetBlobPropertiesAsync()`
- Verifies user permissions
- Returns validation errors with field context

**BlobCopyService**
- Streams blob data in chunks (1 MB default)
- Tracks operation state (Pending → Running → Completed/Failed/Cancelled)
- Broadcasts progress updates via SignalR
- Supports cancellation via `CancellationToken`
- Handles errors gracefully

**ProgressNotificationService**
- Broadcasts progress via SignalR hub
- Manages operation groups for targeted messaging
- Handles connection lifecycle events

#### SignalR Hub (BlobCopyHub)

```csharp
public class BlobCopyHub : Hub
{
  // Server → Client (push events)
  public async Task ProgressUpdate(ProgressUpdate progress)
  public async Task CopyCompleted(BlobCopyOperation operation)
  public async Task CopyFailed(BlobCopyOperation operation)
  public async Task OperationCancelled(BlobCopyOperation operation)
  
  // Client → Server (receive requests)
  public async Task JoinOperationGroup(string operationId)
  public async Task LeaveOperationGroup(string operationId)
}
```

#### Data Models

**BlobCopyOperation**
```csharp
public class BlobCopyOperation
{
  public string Id { get; set; }              // Unique ID
  public string SourceUri { get; set; }
  public string DestinationUri { get; set; }
  public BlobCopyStatus Status { get; set; }
  public long BytesCopied { get; set; }
  public long TotalBytes { get; set; }
  public DateTime CreatedAt { get; set; }
  public DateTime? CompletedAt { get; set; }
  public List<ValidationError> Errors { get; set; }
}
```

**BlobCopyStatus** (Enum)
```
Pending   → Running → Completed
                   → Failed
                   → Cancelled
```

---

## Data Flow

### Copy Operation Sequence

```
1. User enters source/destination URIs
   ↓
2. Frontend calls POST /validate
   ↓
3. Backend validates blobs exist & accessible
   ↓
4. User clicks "Start Copy"
   ↓
5. Frontend calls POST /start
   ↓
6. Backend creates operation record (Pending)
   ↓
7. Backend returns 201 Created with operation
   ↓
8. Backend starts background task for copying
   ↓
9. Task transitions to Running status
   ↓
10. Chunks of data copied from source → destination
    - Each chunk triggers ProgressUpdate event
    ↓
11. Frontend receives updates via SignalR
    - Updates percentage, bytes, ETA
    ↓
12. Copy completes or fails/cancelled
    ↓
13. CopyCompleted/CopyFailed/OperationCancelled event
    ↓
14. Operation record updated with final status
    ↓
15. Frontend displays result
```

### Real-Time Progress Updates

**WebSocket (Preferred)**
```
Backend copies chunk → ProgressNotificationService
  → BlobCopyHub.ProgressUpdate()
  → SignalR broadcast to operation group
  → Frontend receives via connection.on('ProgressUpdate')
  → useCopyOperation hook updates state
  → useProgress hook recalculates metrics
  → ProgressDisplay re-renders
```

**HTTP Polling (Fallback)**
```
Frontend timer (1s interval)
  → GET /api/blobcopy/status/{id}
  → Backend returns current operation
  → Frontend updates state
  → useProgress hook recalculates
  → ProgressDisplay re-renders
```

---

## Scalability Considerations

### Horizontal Scaling

**Stateless API**
- Each request is independent
- No in-memory operation state
- Can scale to multiple API servers behind load balancer

**Distributed SignalR**
- Use Azure SignalR Service for multi-instance deployments
- Automatic client redistribution on server shutdown
- Backplane for cross-server messaging

### Performance Optimizations

**Chunked Copying**
- 1 MB chunks prevent memory exhaustion
- Enables progress reporting without full blob in memory
- Tolerates network interruptions within chunks

**Connection Pooling**
- Azure SDK manages blob client connections
- Reuse HTTP connections for better throughput

**Caching**
- Consider caching blob metadata (size, exists)
- Reduce validation latency for repeated blobs

### Limitations

**Memory Usage**
- Current: O(chunk size) = ~1 MB
- Can support files up to Azure limits (4.75 TB for blob storage)

**Transfer Rate**
- Limited by network bandwidth between client/server and server/Azure
- Expected: 100+ MB/s on modern networks

**Concurrent Operations**
- Limited by server resources (CPU, memory, connections)
- Can support 100+ concurrent copies with adequate resources

---

## Security Architecture

### Authentication Flow

```
User logs in (future: OAuth/AD)
  ↓
Frontend receives token
  ↓
Frontend includes in all API requests
  ↓
Backend validates token
  ↓
Backend uses DefaultAzureCredential for Azure access
```

### Authorization

- **Source blob**: Requires read access
- **Destination container**: Requires write access
- Azure RBAC controls actual permissions

### Error Handling

- Server errors don't leak sensitive information
- Validation errors show field + generic message
- Connection failures gracefully degrade to polling

---

## Testing Strategy

### Unit Tests (Vitest Frontend, xUnit Backend)
- Service methods with mocked dependencies
- Hook behavior with React Testing Library
- Component rendering with mocked hooks

### Integration Tests (xUnit)
- Full endpoint testing with mocked Azure services
- Controller → Service → Azure SDK flow
- Error paths and state transitions

### E2E Tests (Playwright)
- Real browser automation
- Multi-browser coverage (Chrome, Firefox, Safari)
- User workflows: validate → start → progress → complete

### Coverage Goals
- **Backend**: ≥85% (125+ test cases)
- **Frontend**: ≥85% (100+ test cases)
- **E2E**: 7 core scenarios

---

## Deployment Architecture

### Container Strategy

```dockerfile
# Backend
FROM mcr.microsoft.com/dotnet/aspnet:10.0
COPY --from=builder /app .
ENTRYPOINT ["dotnet", "BlobCopy.API.dll"]

# Frontend
FROM node:18 AS builder
RUN npm install && npm run build

FROM nginx:alpine
COPY --from=builder /app/dist /usr/share/nginx/html
```

### Orchestration Options

**Azure Container Instances (ACI)**
- Simple deployment for low traffic
- Pay-per-second billing

**Azure Kubernetes Service (AKS)**
- Auto-scaling for high load
- Service discovery, health checks, rolling updates

**Azure App Service**
- Managed container hosting
- Built-in authentication, monitoring

---

## Monitoring & Observability

### Logging
- RequestLoggingMiddleware logs all requests with correlation ID
- Structured logging (JSON) for easy parsing
- Application Insights integration for cloud monitoring

### Metrics
- Operation duration, success/failure rates
- Transfer rates per operation
- WebSocket connection lifecycle

### Error Tracking
- Sentry/Azure Monitor for exception aggregation
- Alert on validation/copy failures
- Dashboard for operation insights

---

## Future Enhancements

1. **OAuth/OpenID Connect Authentication**
2. **Batch Operations** (copy multiple blobs)
3. **Copy Between Azure Clouds** (China, Government)
4. **Azure Data Lake Support**
5. **Scheduled Copies** (CRON)
6. **Copy History & Analytics**
7. **Resume Failed Copies**
8. **Incremental Snapshots**

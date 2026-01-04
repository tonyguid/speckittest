# Blob Copy API Documentation

## Base URL

```
http://localhost:5000
```

## Authentication

Currently, the API uses Azure Storage credentials configured in `appsettings.json`. The credentials are used to authenticate requests to Azure Blob Storage.

For production deployments, consider adding:
- OAuth 2.0 / OpenID Connect
- API Key authentication
- Azure AD B2C integration

## Data Types

### CopyRequest

```typescript
{
  sourceUri: string;        // Full URI to source blob
  destinationUri: string;   // Full URI to destination blob
}
```

### BlobCopyOperation

```typescript
{
  id: string;                    // Unique operation identifier
  sourceUri: string;             // Source blob URI
  destinationUri: string;        // Destination blob URI
  status: BlobCopyStatus;        // Current operation status
  bytesCopied: number;          // Bytes copied so far
  totalBytes: number;           // Total blob size
  createdAt: string;            // ISO 8601 timestamp
  completedAt?: string;         // ISO 8601 timestamp (when finished)
  errors?: ValidationError[];   // Errors (if failed)
}
```

### BlobCopyStatus Enum

```typescript
enum BlobCopyStatus {
  Pending = "Pending",        // Waiting to start
  Running = "Running",        // Copy in progress
  Completed = "Completed",    // Successfully completed
  Failed = "Failed",          // Copy failed with errors
  Cancelled = "Cancelled"     // Manually cancelled
}
```

### ValidationError

```typescript
{
  field: string;      // Field that failed validation
  message: string;    // Error message
}
```

---

## Endpoints

### POST /api/blobcopy/validate

Validate blob URIs and permissions without starting the copy.

**Request Body:**
```json
{
  "sourceUri": "https://account.blob.core.windows.net/source/file.vhd",
  "destinationUri": "https://account.blob.core.windows.net/dest/file-copy.vhd"
}
```

**Success Response (200 OK):**
```json
[]  // Empty array = valid
```

```json
[
  {
    "field": "sourceUri",
    "message": "Blob not found at source location"
  },
  {
    "field": "permissions",
    "message": "Access denied to source container"
  }
]
```

**Error Response (500 Internal Server Error):**
```json
{
  "message": "Internal server error"
}
```

**Validation Checks:**
- ✓ Source blob exists and is accessible
- ✓ Destination container exists and is writable
- ✓ Source and destination are different
- ✓ Valid Azure Storage URI format
- ✓ Proper authentication/authorization

---

### POST /api/blobcopy/start

Initiate a blob copy operation.

**Request Body:**
```json
{
  "sourceUri": "https://account.blob.core.windows.net/source/file.vhd",
  "destinationUri": "https://account.blob.core.windows.net/dest/file-copy.vhd"
}
```

**Success Response (201 Created):**
```json
{
  "id": "op-550e8400-e29b-41d4-a716-446655440000",
  "sourceUri": "https://account.blob.core.windows.net/source/file.vhd",
  "destinationUri": "https://account.blob.core.windows.net/dest/file-copy.vhd",
  "status": "Pending",
  "bytesCopied": 0,
  "totalBytes": 1073741824,
  "createdAt": "2026-01-04T10:30:00Z"
}
```

**Response Headers:**
```
Location: /api/blobcopy/status/op-550e8400-e29b-41d4-a716-446655440000
```

**Error Response (400 Bad Request):**
```json
[
  {
    "field": "sourceUri",
    "message": "Blob not found at source location"
  }
]
```

**Behavior:**
- Validates blobs first (same checks as `/validate`)
- Creates operation record with initial status
- Returns immediately (doesn't wait for copy to complete)
- Starts background task for actual copying
- Progress updates sent via SignalR WebSocket

---

### GET /api/blobcopy/status/{operationId}

Get current status of a copy operation.

**Parameters:**
- `operationId` (string, required): Operation ID from `/start` response

**Success Response (200 OK):**
```json
{
  "id": "op-550e8400-e29b-41d4-a716-446655440000",
  "sourceUri": "https://account.blob.core.windows.net/source/file.vhd",
  "destinationUri": "https://account.blob.core.windows.net/dest/file-copy.vhd",
  "status": "Running",
  "bytesCopied": 536870912,
  "totalBytes": 1073741824,
  "createdAt": "2026-01-04T10:30:00Z"
}
```

**Error Response (404 Not Found):**
```json
{
  "message": "Operation not found"
}
```

**Status Progression:**
1. `Pending` - Operation created, copy starting
2. `Running` - Copy in progress, progress updates available
3. `Completed` - Copy finished successfully
4. `Failed` - Copy failed, check `errors` field
5. `Cancelled` - Copy was cancelled by user

---

### POST /api/blobcopy/cancel/{operationId}

Cancel an in-progress copy operation.

**Parameters:**
- `operationId` (string, required): Operation ID to cancel

**Success Response (200 OK):**
```json
{
  "id": "op-550e8400-e29b-41d4-a716-446655440000",
  "sourceUri": "https://account.blob.core.windows.net/source/file.vhd",
  "destinationUri": "https://account.blob.core.windows.net/dest/file-copy.vhd",
  "status": "Cancelled",
  "bytesCopied": 536870912,
  "totalBytes": 1073741824,
  "createdAt": "2026-01-04T10:30:00Z",
  "completedAt": "2026-01-04T10:35:15Z"
}
```

**Error Response (404 Not Found):**
```json
{
  "message": "Operation not found"
}
```

**Behavior:**
- ✓ Works for `Pending` and `Running` operations
- ✓ Idempotent (cancelling already completed operation is safe)
- ✓ Updates operation status to `Cancelled`
- ✓ Notifies clients via SignalR `OperationCancelled` event
- ✓ Preserves progress (bytes copied before cancellation)

---

### GET /health

Health check endpoint for API availability.

**Success Response (200 OK):**
```json
{
  "status": "healthy"
}
```

**Response Headers:**
```
Content-Type: application/json
```

**Use Cases:**
- Verify API is running before UI operations
- Load balancer health checks
- Monitoring and alerting

---

## SignalR WebSocket Events

Real-time progress updates via SignalR hub at `/blobcopyhub`.

### Client → Server Methods

#### JoinOperationGroup
Subscribe to updates for a specific operation.

```typescript
await connection.invoke('JoinOperationGroup', 'op-550e8400-e29b-41d4-a716-446655440000');
```

#### LeaveOperationGroup
Unsubscribe from operation updates.

```typescript
await connection.invoke('LeaveOperationGroup', 'op-550e8400-e29b-41d4-a716-446655440000');
```

### Server → Client Events

#### ProgressUpdate
Sent periodically while copy is running (approximately every 1-5 seconds).

```typescript
connection.on('ProgressUpdate', (progress: {
  operationId: string;
  bytesCopied: number;
  totalBytes: number;
}) => {
  console.log(`Progress: ${progress.bytesCopied}/${progress.totalBytes} bytes`);
});
```

#### CopyCompleted
Sent when copy finishes successfully.

```typescript
connection.on('CopyCompleted', (operation: BlobCopyOperation) => {
  console.log('Copy completed:', operation);
});
```

#### CopyFailed
Sent when copy fails with errors.

```typescript
connection.on('CopyFailed', (operation: BlobCopyOperation) => {
  console.log('Copy failed:', operation.errors);
});
```

#### OperationCancelled
Sent when operation is cancelled.

```typescript
connection.on('OperationCancelled', (operation: BlobCopyOperation) => {
  console.log('Operation cancelled');
});
```

---

## Error Handling

### Common HTTP Status Codes

| Code | Meaning | Handling |
|------|---------|----------|
| 200 | OK | Success, use response data |
| 201 | Created | Operation created, use Location header for status URL |
| 400 | Bad Request | Validation failed, show errors to user |
| 404 | Not Found | Operation doesn't exist, may have expired |
| 500 | Server Error | Internal error, retry later or contact support |

### Validation Error Fields

| Field | Meaning |
|-------|---------|
| `sourceUri` | Source blob URI is invalid/inaccessible |
| `destinationUri` | Destination container is invalid/unwritable |
| `permissions` | User lacks required Azure Storage permissions |
| `format` | URI format is invalid |

---

## Rate Limiting

Currently no rate limiting is implemented. For production:

- Implement rate limiting (e.g., 10 requests/second per user)
- Consider pricing impact of large blob copies
- Add request throttling for Azure Storage API quotas

---

## Examples

### Example 1: Complete Copy Workflow

```typescript
// 1. Validate
const response = await fetch('http://localhost:5000/api/blobcopy/validate', {
  method: 'POST',
  body: JSON.stringify({
    sourceUri: 'https://account.blob.core.windows.net/source/file.vhd',
    destinationUri: 'https://account.blob.core.windows.net/dest/file-copy.vhd'
  })
});

if (response.ok) {
  const errors = await response.json();
  if (errors.length > 0) {
    console.error('Validation failed:', errors);
    return;
  }
}

// 2. Start copy
const startResponse = await fetch('http://localhost:5000/api/blobcopy/start', {
  method: 'POST',
  body: JSON.stringify({
    sourceUri: 'https://account.blob.core.windows.net/source/file.vhd',
    destinationUri: 'https://account.blob.core.windows.net/dest/file-copy.vhd'
  })
});

const operation = await startResponse.json();
console.log('Copy started:', operation.id);

// 3. Poll for status
const pollStatus = async () => {
  const statusResponse = await fetch(
    `http://localhost:5000/api/blobcopy/status/${operation.id}`
  );
  const current = await statusResponse.json();
  console.log(`Progress: ${current.bytesCopied}/${current.totalBytes}`);
  
  if (current.status === 'Completed' || current.status === 'Failed') {
    return current;
  }
  
  // Poll again in 2 seconds
  setTimeout(pollStatus, 2000);
};

await pollStatus();
```

### Example 2: Cancel Operation

```typescript
const cancelResponse = await fetch(
  `http://localhost:5000/api/blobcopy/cancel/${operationId}`,
  { method: 'POST' }
);

if (cancelResponse.ok) {
  const cancelled = await cancelResponse.json();
  console.log('Cancelled at:', cancelled.completedAt);
}
```

---

## Changelog

### Version 1.0 (2026-01-04)
- Initial API release
- 5 REST endpoints
- SignalR real-time progress
- Comprehensive validation

# SignalR Events Reference: Blob Copy Feature

**Created**: January 4, 2026
**Status**: Complete

## Overview

This document defines all SignalR (WebSocket) events for real-time blob copy progress tracking. SignalR provides low-latency bidirectional communication between backend and frontend.

---

## Hub: BlobCopyHub

**Endpoint**: `wss://api.example.com/signalr/blob-copy-hub`

### Server → Client Events (Pushed by Server)

#### 1. ProgressUpdate

**Frequency**: Every 1-5 seconds during active copy

**When Sent**: Copy is in-progress

**Payload**:
```typescript
interface ProgressUpdate {
  /// Unique operation ID
  copyOperationId: string;
  
  /// Bytes copied in this interval
  bytesTransferred: number;
  
  /// Total blob size in bytes
  totalBytes: number;
  
  /// Progress 0-100
  progressPercentage: number;
  
  /// Remaining time in seconds (null if unknown)
  estimatedSecondsRemaining?: number;
  
  /// Server timestamp (ISO 8601)
  updatedAt: string;
  
  /// Transfer rate in MB/s
  transferRateMbps: number;
}
```

**Example**:
```json
{
  "copyOperationId": "a1b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d",
  "bytesTransferred": 104857600,
  "totalBytes": 1073741824,
  "progressPercentage": 9.77,
  "estimatedSecondsRemaining": 42,
  "updatedAt": "2026-01-04T14:30:45.123Z",
  "transferRateMbps": 24.5
}
```

**Client Implementation** (TypeScript/React):
```typescript
useEffect(() => {
  const connection = new signalR.HubConnectionBuilder()
    .withUrl("wss://api.example.com/signalr/blob-copy-hub", {
      accessTokenFactory: () => getAuthToken()
    })
    .withAutomaticReconnect()
    .build();

  connection.on("ProgressUpdate", (update: ProgressUpdate) => {
    setProgress(update.progressPercentage);
    setEstimatedTime(update.estimatedSecondsRemaining);
    setBytesTransferred(update.bytesTransferred);
  });

  connection.start();
}, []);
```

---

#### 2. CopyCompleted

**When Sent**: Copy operation finished successfully

**Payload**:
```typescript
interface CopyCompletedResult {
  /// Unique operation ID
  copyOperationId: string;
  
  /// Destination URI where blob was copied
  destinationUri: string;
  
  /// Total bytes copied
  totalBytes: number;
  
  /// Duration in seconds
  durationSeconds: number;
  
  /// Server timestamp when completed
  completedAt: string;
}
```

**Example**:
```json
{
  "copyOperationId": "a1b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d",
  "destinationUri": "https://mystorage.blob.core.windows.net/dest/myblob.bin",
  "totalBytes": 1073741824,
  "durationSeconds": 45,
  "completedAt": "2026-01-04T14:31:30.456Z"
}
```

**Client Implementation**:
```typescript
connection.on("CopyCompleted", (result: CopyCompletedResult) => {
  setStatus("completed");
  setProgress(100);
  showSuccessMessage(`✅ Copy complete! ${formatBytes(result.totalBytes)} in ${result.durationSeconds}s`);
  showResultMessage({
    type: "success",
    destination: result.destinationUri,
    duration: result.durationSeconds
  });
});
```

---

#### 3. CopyFailed

**When Sent**: Copy operation encountered an error

**Payload**:
```typescript
interface CopyFailedError {
  /// Unique operation ID
  copyOperationId: string;
  
  /// Machine-readable error code
  errorCode: string;
  
  /// Human-readable error message
  errorMessage: string;
  
  /// Whether the operation can be retried
  retryable: boolean;
  
  /// Suggested retry delay in seconds (if retryable)
  retryAfterSeconds?: number;
  
  /// Server timestamp of failure
  failedAt: string;
}
```

**Error Codes**:
- `SOURCE_NOT_FOUND`: Source blob was deleted
- `DESTINATION_INACCESSIBLE`: Lost permission to destination
- `NETWORK_TIMEOUT`: Connection to Azure Storage lost
- `STORAGE_QUOTA_EXCEEDED`: Destination storage account full
- `AUTHENTICATION_FAILED`: Credentials expired or invalid
- `BLOB_TOO_LARGE`: Blob exceeds size limit
- `SERVICE_UNAVAILABLE`: Azure Storage temporarily unavailable

**Example**:
```json
{
  "copyOperationId": "a1b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d",
  "errorCode": "NETWORK_TIMEOUT",
  "errorMessage": "Connection to Azure Blob Storage timed out after 30 seconds",
  "retryable": true,
  "retryAfterSeconds": 5,
  "failedAt": "2026-01-04T14:31:45.789Z"
}
```

**Client Implementation**:
```typescript
connection.on("CopyFailed", (error: CopyFailedError) => {
  setStatus("failed");
  
  if (error.retryable) {
    showErrorMessage(`⚠️ ${error.errorMessage}`, "warning");
    showRetryButton(true);
    setCanRetry(true);
  } else {
    showErrorMessage(`❌ ${error.errorMessage}`, "error");
    showRetryButton(false);
    setCanRetry(false);
  }
});
```

---

#### 4. OperationCancelled

**When Sent**: User cancels an in-progress copy operation

**Payload**:
```typescript
interface OperationCancelledResult {
  /// Unique operation ID
  copyOperationId: string;
  
  /// Bytes transferred before cancellation
  bytesTransferred: number;
  
  /// Server timestamp of cancellation
  cancelledAt: string;
  
  /// Message to user
  message: string;
}
```

**Example**:
```json
{
  "copyOperationId": "a1b2c3d4-e5f6-4a8b-9c0d-1e2f3a4b5c6d",
  "bytesTransferred": 536870912,
  "cancelledAt": "2026-01-04T14:31:10.000Z",
  "message": "Copy operation cancelled by user"
}
```

**Client Implementation**:
```typescript
connection.on("OperationCancelled", (result: OperationCancelledResult) => {
  setStatus("cancelled");
  setProgress(0);
  showMessage(`⊘ ${result.message}. ${formatBytes(result.bytesTransferred)} was transferred.`, "info");
});
```

---

#### 5. ConnectionEstablished

**When Sent**: Immediately after successful WebSocket connection

**Payload**:
```typescript
interface ConnectionEstablishedMessage {
  message: string;
  serverTime: string;
  connectionId: string;
}
```

**Example**:
```json
{
  "message": "Connected to Blob Copy Hub",
  "serverTime": "2026-01-04T14:30:00.000Z",
  "connectionId": "AbC123DeFgHiJkLmNoPqRsTuVwXyZ"
}
```

---

### Client → Server Actions (Optional for MVP)

Currently, no client-to-server actions are required. Progress streaming is one-directional.

**Future Enhancement**: Could add:
- `RequestStatusUpdate()`: Force immediate status refresh
- `RequestCancelOperation(operationId)`: Request cancellation
- `PauseOperation()`: Pause copy (not implemented)

---

## Connection Management

### Connecting

```typescript
const connection = new signalR.HubConnectionBuilder()
  .withUrl("wss://api.example.com/signalr/blob-copy-hub", {
    accessTokenFactory: async () => {
      return await getAuthToken(); // Get JWT from MSAL
    },
    transport: signalR.HttpTransportType.WebSockets,
    logMessageContent: false
  })
  .withAutomaticReconnect([0, 0, 1000, 2000, 5000, 10000])
  .build();

connection.on("ProgressUpdate", handleProgressUpdate);
connection.on("CopyCompleted", handleCopyCompleted);
connection.on("CopyFailed", handleCopyFailed);

await connection.start();
```

### Subscribing to a Copy Operation

```typescript
// After starting copy, subscribe to progress updates
await connection.invoke("JoinCopyOperation", copyOperationId);
```

### Unsubscribing

```typescript
await connection.invoke("LeaveCopyOperation", copyOperationId);
```

### Disconnecting

```typescript
await connection.stop();
```

### Reconnection Behavior

- Automatic reconnect attempts: [0ms, 0ms, 1s, 2s, 5s, 10s]
- After successful reconnect: Auto-resubscribe to active operations
- If connection lost: Fall back to polling `/status` endpoint

---

## Error Handling

### Connection Errors

```typescript
connection.onreconnecting((error) => {
  console.warn(`Connection lost, attempting to reconnect: ${error}`);
  setConnectionStatus("reconnecting");
});

connection.onreconnected(async (connectionId) => {
  console.log(`Reconnected with ID: ${connectionId}`);
  setConnectionStatus("connected");
  // Resubscribe to active operations
  if (activeCopyId) {
    await connection.invoke("JoinCopyOperation", activeCopyId);
  }
});

connection.onclose((error) => {
  console.error(`Connection closed: ${error}`);
  setConnectionStatus("disconnected");
  // Switch to polling fallback
  startPollingStatusEndpoint();
});
```

### Polling Fallback

If WebSocket disconnects and cannot reconnect:

```typescript
const pollStatus = async () => {
  const response = await fetch(`/api/v1/blob-copy/${copyId}/status`, {
    headers: { Authorization: `Bearer ${token}` }
  });
  const operation = await response.json();
  updateProgressUI(operation);
  
  if (operation.status === "in-progress") {
    setTimeout(pollStatus, 2000); // Poll every 2 seconds
  }
};
```

---

## Message Size & Performance

- Average `ProgressUpdate`: ~200 bytes
- Frequency: Every 2 seconds for 100MB blob = ~100 messages
- Total bandwidth: ~20 KB for 100MB copy
- Negligible impact compared to blob transfer bandwidth

---

## Security

- All messages encrypted via WSS (TLS)
- JWT token required in initial HTTP handshake
- Operations are user-specific (no cross-user data leakage)
- Server validates user ownership of operation before sending updates


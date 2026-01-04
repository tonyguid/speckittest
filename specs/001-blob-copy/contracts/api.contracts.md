# API Contracts: Blob Copy Feature

**Created**: January 4, 2026
**Version**: 1.0
**Status**: Complete

## OpenAPI 3.0.0 Specification

```yaml
openapi: 3.0.0
info:
  title: Blob Copy API
  version: 1.0.0
  description: |
    REST API for copying Azure Blob Storage blobs with real-time progress tracking.
    Supports source URI and destination URI validation, copy operation initiation,
    and status polling. Real-time progress updates via SignalR WebSocket.
  contact:
    name: Support
  license:
    name: MIT

servers:
  - url: https://api.example.com/api/v1
    description: Production API
  - url: http://localhost:5000/api/v1
    description: Local development API

tags:
  - name: Validation
    description: Validate blob URIs before copy
  - name: Copy Operations
    description: Manage blob copy operations
  - name: Status
    description: Query copy operation status

paths:
  /blob-copy/validate:
    post:
      tags:
        - Validation
      summary: Validate blob URIs
      description: |
        Validate source and destination blob URIs for format, existence, and permissions.
        Does NOT initiate a copy; only validates inputs.
      operationId: validateBlobUris
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/ValidateRequest'
            examples:
              valid:
                summary: Valid URI pair
                value:
                  sourceUri: https://mystorage.blob.core.windows.net/source/myblob.txt
                  destinationUri: https://mystorage.blob.core.windows.net/dest/myblob.txt
      responses:
        '200':
          description: Validation result
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ValidateResponse'
              examples:
                valid:
                  summary: Valid URIs
                  value:
                    valid: true
                    errors: []
                invalid:
                  summary: Invalid URIs with errors
                  value:
                    valid: false
                    errors:
                      - field: sourceUri
                        message: Blob not found
                        code: SOURCE_NOT_FOUND
        '400':
          description: Bad request (malformed JSON, missing fields)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '401':
          description: Unauthorized (not authenticated)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '503':
          description: Service unavailable (Azure Storage unreachable)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'

  /blob-copy/start:
    post:
      tags:
        - Copy Operations
      summary: Start a blob copy operation
      description: |
        Initiate a blob copy operation from source to destination.
        Performs validation and returns immediately with operation ID.
        Copy happens asynchronously; client should poll /status or use SignalR for updates.
      operationId: startBlobCopy
      requestBody:
        required: true
        content:
          application/json:
            schema:
              $ref: '#/components/schemas/CopyRequest'
      responses:
        '202':
          description: Copy operation accepted and queued
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/BlobCopyOperation'
        '400':
          description: Validation failed (invalid URIs, format issues)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ValidationErrorResponse'
        '401':
          description: Unauthorized
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '409':
          description: Conflict (destination already exists and user action required)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '503':
          description: Service unavailable
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'

  /blob-copy/{id}/status:
    get:
      tags:
        - Status
      summary: Get copy operation status
      description: |
        Retrieve current status of a blob copy operation by ID.
        Includes progress percentage, bytes transferred, and error details if failed.
      operationId: getCopyStatus
      parameters:
        - name: id
          in: path
          description: Copy operation ID (UUID)
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Current operation status
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/BlobCopyOperation'
        '404':
          description: Operation not found
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '401':
          description: Unauthorized
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'

  /blob-copy/{id}/cancel:
    post:
      tags:
        - Copy Operations
      summary: Cancel a copy operation
      description: |
        Request cancellation of an in-progress copy operation.
        Operation transitions to 'cancelled' state.
        Destination blob may have partial data depending on cancellation timing.
      operationId: cancelCopyOperation
      parameters:
        - name: id
          in: path
          description: Copy operation ID (UUID)
          required: true
          schema:
            type: string
            format: uuid
      responses:
        '200':
          description: Cancellation request accepted
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/BlobCopyOperation'
        '404':
          description: Operation not found
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '409':
          description: Operation cannot be cancelled (already completed or failed)
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'
        '401':
          description: Unauthorized
          content:
            application/json:
              schema:
                $ref: '#/components/schemas/ErrorResponse'

  /health:
    get:
      tags:
        - Health
      summary: Health check endpoint
      description: Simple health check for load balancers and monitoring
      operationId: healthCheck
      responses:
        '200':
          description: Service is healthy
          content:
            application/json:
              schema:
                type: object
                properties:
                  status:
                    type: string
                    example: healthy
        '503':
          description: Service is unhealthy
          content:
            application/json:
              schema:
                type: object
                properties:
                  status:
                    type: string
                    example: unhealthy

components:
  schemas:
    ValidateRequest:
      type: object
      required:
        - sourceUri
        - destinationUri
      properties:
        sourceUri:
          type: string
          format: uri
          description: Source blob URI
          example: https://mystorage.blob.core.windows.net/source/myblob.txt
        destinationUri:
          type: string
          format: uri
          description: Destination blob URI
          example: https://mystorage.blob.core.windows.net/dest/myblob.txt

    ValidateResponse:
      type: object
      required:
        - valid
        - errors
      properties:
        valid:
          type: boolean
          description: Whether both URIs are valid
        errors:
          type: array
          items:
            $ref: '#/components/schemas/ValidationError'

    ValidationError:
      type: object
      properties:
        field:
          type: string
          enum:
            - sourceUri
            - destinationUri
        message:
          type: string
          description: User-friendly error message
          example: Source blob not found
        code:
          type: string
          description: Error code for programmatic handling
          enum:
            - INVALID_URI_FORMAT
            - IDENTICAL_URIS
            - SOURCE_NOT_FOUND
            - DESTINATION_INACCESSIBLE
            - INVALID_CONTAINER_NAME
            - EMPTY_FIELD

    CopyRequest:
      type: object
      required:
        - sourceUri
        - destinationUri
      properties:
        sourceUri:
          type: string
          format: uri
          description: Source blob URI to copy from
        destinationUri:
          type: string
          format: uri
          description: Destination blob URI to copy to
        overwriteIfExists:
          type: boolean
          default: false
          description: If true and destination exists, overwrite it. If false, fail with error.

    BlobCopyOperation:
      type: object
      required:
        - id
        - sourceUri
        - destinationUri
        - status
        - progressPercentage
        - startedAt
        - correlationId
      properties:
        id:
          type: string
          format: uuid
          description: Unique operation ID
        sourceUri:
          type: string
          format: uri
        destinationUri:
          type: string
          format: uri
        status:
          type: string
          enum:
            - pending
            - in-progress
            - completed
            - failed
            - cancelled
          description: Current operation status
        progressPercentage:
          type: number
          format: double
          minimum: 0
          maximum: 100
          description: Progress as percentage (0-100)
        bytesTransferred:
          type: integer
          format: int64
          description: Bytes transferred so far
        totalBytes:
          type: integer
          format: int64
          description: Total bytes to transfer
        estimatedTimeRemaining:
          type: integer
          description: Estimated seconds remaining (null if indeterminate)
          nullable: true
        errorMessage:
          type: string
          description: Error message if failed (null if successful or pending)
          nullable: true
        errorCode:
          type: string
          description: Error code for programmatic handling
          nullable: true
        startedAt:
          type: string
          format: date-time
          description: ISO 8601 timestamp when operation started
        completedAt:
          type: string
          format: date-time
          description: ISO 8601 timestamp when operation completed (null if still in progress)
          nullable: true
        correlationId:
          type: string
          description: Correlation ID for distributed tracing

    ErrorResponse:
      type: object
      required:
        - error
      properties:
        error:
          type: string
          description: Error message

    ValidationErrorResponse:
      type: object
      required:
        - error
        - errors
      properties:
        error:
          type: string
          example: Validation failed
        errors:
          type: array
          items:
            $ref: '#/components/schemas/ValidationError'

  securitySchemes:
    bearerAuth:
      type: http
      scheme: bearer
      bearerFormat: JWT
      description: Azure Entra ID JWT token

security:
  - bearerAuth: []
```

---

## SignalR Hub: BlobCopyHub

### Overview

Real-time bidirectional communication for progress updates during blob copy operations.

### Connection URL

```
wss://api.example.com/signalr/blob-copy-hub
```

### Client Methods (called by server)

#### OnProgressUpdate

Sent every 1-5 seconds during active copy operation.

**Signature**:
```typescript
onProgressUpdate(update: ProgressUpdate)
```

**Parameters**:
```typescript
interface ProgressUpdate {
  copyOperationId: string;      // UUID of the copy operation
  bytesTransferred: number;      // Bytes transferred so far
  totalBytes: number;            // Total blob size
  progressPercentage: number;    // 0-100
  estimatedSecondsRemaining?: number;  // Calculated ETA
  updatedAt: string;             // ISO 8601 timestamp
  transferRateMbps: number;      // MB/s for display
}
```

**Example**:
```json
{
  "copyOperationId": "550e8400-e29b-41d4-a716-446655440000",
  "bytesTransferred": 52428800,
  "totalBytes": 104857600,
  "progressPercentage": 50.0,
  "estimatedSecondsRemaining": 45,
  "updatedAt": "2026-01-04T12:30:45.123Z",
  "transferRateMbps": 23.5
}
```

#### OnCopyCompleted

Sent when blob copy completes successfully.

**Signature**:
```typescript
onCopyCompleted(result: CopyCompletedResult)
```

**Parameters**:
```typescript
interface CopyCompletedResult {
  copyOperationId: string;
  destinationUri: string;
  totalBytes: number;
  completedAt: string;  // ISO 8601
}
```

#### OnCopyFailed

Sent when blob copy fails.

**Signature**:
```typescript
onCopyFailed(error: CopyError)
```

**Parameters**:
```typescript
interface CopyError {
  copyOperationId: string;
  errorCode: string;
  errorMessage: string;
  retryable: boolean;
  failedAt: string;  // ISO 8601
}
```

#### OnOperationCancelled

Sent when user cancels an in-progress operation.

**Signature**:
```typescript
onOperationCancelled(result: CancellationResult)
```

**Parameters**:
```typescript
interface CancellationResult {
  copyOperationId: string;
  cancelledAt: string;
  bytesTransferred: number;
}
```

### Server Methods (called by client)

None required for MVP. Progress updates are unidirectional.

### Connection Lifecycle

1. Client connects to SignalR hub
2. Client sends `JoinCopyOperation(copyOperationId)` to subscribe to updates
3. Server sends progress updates as `OnProgressUpdate` every ~2 seconds
4. When operation completes, server sends `OnCopyCompleted` or `OnCopyFailed`
5. Client disconnects or subscribes to another operation

### Authentication

SignalR connection inherits JWT token from HTTP request header:
```
Authorization: Bearer <Azure Entra ID JWT>
```

### Error Handling

- If connection drops during copy: client polls `/status` endpoint for latest state
- No messages lost if temporary disconnection; client can sync with last status
- Server doesn't queue messages; only sends current progress

---

## HTTP Status Codes

| Code | Meaning | Cause |
|------|---------|-------|
| 200 | OK | Successful GET request |
| 202 | Accepted | Copy operation queued successfully |
| 400 | Bad Request | Malformed request, validation errors |
| 401 | Unauthorized | Missing or invalid JWT token |
| 404 | Not Found | Operation ID doesn't exist |
| 409 | Conflict | Destination exists, overwrite conflict |
| 503 | Service Unavailable | Azure Blob Storage unreachable |

---

## Rate Limiting

Not enforced in MVP, but recommended for production:
- Per user: 100 copy operations per hour
- Per API: 1000 operations per minute
- Burst: 10 operations per second

---

## Correlation & Tracing

Every response includes `x-correlation-id` header:
```
x-correlation-id: 550e8400-e29b-41d4-a716-446655440000
```

Use this ID to correlate logs and Application Insights traces across frontend, backend, and Azure services.


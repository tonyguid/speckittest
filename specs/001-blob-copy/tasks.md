# Phase 2: Development Tasks – Blob Copy Feature

**Created**: January 4, 2026  
**Feature**: Blob Storage Copy (001-blob-copy)  
**Stack**: C# .NET 10, React 18+, xUnit, Vitest, Playwright, SignalR, Azure Blob Storage  
**Spec Ref**: [spec.md](spec.md), [plan.md](plan.md), [data-model.md](data-model.md), [API Contracts](contracts/api.contracts.md)

---

## Overview of Phase 2

Phase 2 transforms the Phase 1 design (data model, API contracts, architecture) into production-ready code. This phase delivers a fully functional blob copy feature with:

- **Backend**: C# .NET 10 REST API with 5 endpoints (validate, start, status, cancel, health)
- **Backend Services**: BlobCopyService (core copy logic), BlobValidationService (URI/permission validation), ProgressNotificationService (SignalR updates)
- **Backend Tests**: xUnit unit and integration tests with ≥80% coverage of core services
- **Frontend**: React 18+ SPA with input form, real-time progress display, error handling
- **Frontend Services**: useBlobCopy hook, useSignalR hook, API service, SignalR service
- **Frontend Tests**: Vitest unit tests (≥80% component/service coverage)
- **E2E Tests**: Playwright cross-browser tests covering all 3 user stories
- **CI/CD & Documentation**: GitHub Actions workflow, health check endpoint, OpenAPI docs

**Success Metrics**:
- All acceptance criteria met for 3 user stories
- ≥80% test coverage on backend services
- ≥80% test coverage on frontend components/hooks
- All 5 API endpoints operational with proper error handling
- Real-time progress updates via SignalR ≥ every 5 seconds
- E2E tests passing on Chrome, Firefox, Safari
- API response latency ≤ 200ms p95 per constitution

---

## Task Breakdown by Category

### Backend Infrastructure (Models, SignalR Setup)

---

#### TASK-B-001: Create Core Data Models (C#)

**Category**: Backend Infrastructure  
**Description**: Implement C# entity classes for BlobCopyOperation, ProgressUpdate, ValidationError, and BlobCopyStatus enum. Models must match data-model.md specifications. Include XML documentation for all public members.

**Acceptance Criteria**:
- [ ] BlobCopyOperation class with all required properties (Id, SourceUri, DestinationUri, Status, ProgressPercentage, BytesTransferred, TotalBytes, EstimatedTimeRemaining, ErrorMessage, ErrorCode, StartedAt, CompletedAt, CorrelationId, InitiatedBy, CancellationRequested)
- [ ] BlobCopyStatus enum (Pending, InProgress, Completed, Failed, Cancelled) with XML documentation
- [ ] ProgressUpdate class with required properties (CopyOperationId, BytesTransferred, TotalBytes, ProgressPercentage, EstimatedSecondsRemaining, UpdatedAt, TransferRateMbps)
- [ ] ValidationError class with Field, Message, Code properties
- [ ] All classes are serializable (JSON.NET compatible)
- [ ] No validation logic in models (logic in services)

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Models/BlobCopyOperation.cs` (NEW)
- `src/backend/BlobCopy.API/Models/BlobCopyStatus.cs` (NEW)
- `src/backend/BlobCopy.API/Models/ProgressUpdate.cs` (NEW)
- `src/backend/BlobCopy.API/Models/ValidationError.cs` (NEW)
- `src/backend/BlobCopy.API/Models/CopyRequest.cs` (NEW)
- `src/backend/BlobCopy.API/Models/CopyResult.cs` (NEW)

**Dependencies**: None

---

#### TASK-B-002: Configure SignalR Hub (BlobCopyHub)

**Category**: Backend Infrastructure  
**Description**: Implement SignalR hub for real-time progress updates. Hub must support client method calls for progress updates, completion, and error notifications. Include connection tracking and proper cleanup.

**Acceptance Criteria**:
- [ ] BlobCopyHub class inherits from Hub<IBlobCopyClient>
- [ ] OnProgressUpdate method sends ProgressUpdate to connected clients
- [ ] OnCopyCompleted method notifies clients of successful completion
- [ ] OnCopyFailed method sends error details to clients
- [ ] Connection lifecycle properly managed (OnConnectedAsync, OnDisconnectedAsync)
- [ ] Hub registered in Program.cs with proper endpoint configuration
- [ ] IBlobCopyClient interface defined with client methods
- [ ] Hub supports broadcasting to specific client by operation ID

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Hubs/IBlobCopyClient.cs` (NEW)
- `src/backend/BlobCopy.API/Hubs/BlobCopyHub.cs` (NEW)
- `src/backend/BlobCopy.API/Program.cs` (MODIFY - add SignalR registration and mapping)

**Dependencies**: TASK-B-001

---

#### TASK-B-003: Setup Dependency Injection & Configuration

**Category**: Backend Infrastructure  
**Description**: Configure Program.cs with DI container for all services. Register BlobCopyService, BlobValidationService, ProgressNotificationService, Azure SDK clients (BlobContainerClient, BlobClient), and identity providers (DefaultAzureCredential).

**Acceptance Criteria**:
- [ ] BlobCopyService registered as scoped service
- [ ] BlobValidationService registered as scoped service
- [ ] ProgressNotificationService registered as scoped service
- [ ] Azure SDK BlobContainerClient registered with DefaultAzureCredential
- [ ] SignalR hub context registered for ProgressNotificationService
- [ ] Configuration from appsettings.json (Azure Storage account, container names, timeout values)
- [ ] CORS configured to allow frontend requests
- [ ] Error handling middleware registered

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Program.cs` (MODIFY)
- `src/backend/BlobCopy.API/appsettings.json` (NEW/MODIFY)
- `src/backend/BlobCopy.API/appsettings.Development.json` (NEW/MODIFY)

**Dependencies**: TASK-B-001, TASK-B-002

---

---

### Backend Services (BlobCopyService, BlobValidationService, ProgressNotificationService)

---

#### TASK-B-004: Implement BlobValidationService

**Category**: Backend Services  
**Description**: Implement validation service for blob URIs. Validate format, existence, permissions, and business rules (source ≠ destination, container exists, user has access).

**Acceptance Criteria**:
- [ ] ValidateUriFormat method checks URI matches pattern `https://[account].blob.core.windows.net/[container]/[blob]`
- [ ] ValidateBlobExists method attempts to fetch blob properties; returns false if 404 or 401
- [ ] ValidateDestinationWritable method verifies user can write to destination container
- [ ] ValidateIdenticalUris method returns error if source and destination are identical
- [ ] ValidateRequest method orchestrates all checks; returns ValidationResult with list of ValidationError objects
- [ ] Returns specific error codes (INVALID_URI_FORMAT, SOURCE_NOT_FOUND, INSUFFICIENT_PERMISSIONS, IDENTICAL_URIS, etc.)
- [ ] All exceptions caught and mapped to ValidationError
- [ ] No side effects (purely read-only validation)

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Services/BlobValidationService.cs` (NEW)
- `src/backend/BlobCopy.API/Models/ValidationResult.cs` (NEW)

**Dependencies**: TASK-B-001, TASK-B-003

---

#### TASK-B-005: Implement BlobCopyService

**Category**: Backend Services  
**Description**: Implement core blob copy logic. Handle async copy operation, track progress, manage state transitions, and handle cancellation. Use Azure SDK BlobClient.StartCopyFromUriAsync() and polling for progress.

**Acceptance Criteria**:
- [ ] CopyBlobAsync method accepts source and destination URIs
- [ ] Returns BlobCopyOperation object with ID, status, correlation ID
- [ ] Updates BlobCopyOperation state: Pending → InProgress → Completed/Failed/Cancelled
- [ ] Polls blob copy status every 1-2 seconds via BlobProperties.CopyStatus
- [ ] Calculates progress percentage, bytes transferred, estimated time remaining
- [ ] Handles CancellationToken for cancel operation
- [ ] Maps Azure SDK exceptions to user-friendly error messages and error codes
- [ ] Stores operation state in memory (MVP) or database (future)
- [ ] Publishes progress updates via ProgressNotificationService
- [ ] Correlates all logging with CorrelationId for tracing

**Effort**: 5 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Services/BlobCopyService.cs` (NEW)
- `src/backend/BlobCopy.API/Services/CopyOperationManager.cs` (NEW - in-memory operation store)

**Dependencies**: TASK-B-001, TASK-B-003, TASK-B-004

---

#### TASK-B-006: Implement ProgressNotificationService

**Category**: Backend Services  
**Description**: Implement service to send real-time progress updates to connected SignalR clients. Publishes ProgressUpdate events at ~1-5 second intervals during active copy.

**Acceptance Criteria**:
- [ ] PublishProgressAsync method sends ProgressUpdate to all connected clients for given operation ID
- [ ] Calculates transfer rate (MB/s) from bytes transferred and elapsed time
- [ ] Estimates time remaining based on current rate and remaining bytes
- [ ] Broadcasts to specific client group or operation-specific connection (determined by BlobCopyHub)
- [ ] Handles SignalR connection failures gracefully (logs warning, continues)
- [ ] UpdatedAt timestamp is accurate
- [ ] Can be called from BlobCopyService every 1-5 seconds without performance impact

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Services/ProgressNotificationService.cs` (NEW)

**Dependencies**: TASK-B-001, TASK-B-002

---

---

### Backend API Endpoints (5 Endpoints)

---

#### TASK-B-007: Implement POST /api/v1/blob-copy/validate Endpoint

**Category**: Backend API  
**Description**: Implement validation endpoint. Accepts source and destination URIs, validates them, returns validation result with error list if invalid.

**Acceptance Criteria**:
- [ ] Controller: BlobCopyController.ValidateAsync(ValidateRequest)
- [ ] HTTP 200 response: { valid: boolean, errors: ValidationError[] }
- [ ] HTTP 400 response: malformed request (missing fields, invalid JSON)
- [ ] HTTP 401 response: unauthorized (no valid Entra ID token)
- [ ] HTTP 503 response: Azure Storage unreachable
- [ ] Calls BlobValidationService.ValidateRequest
- [ ] No side effects (read-only)
- [ ] Response time ≤ 200ms p95 per constitution

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Controllers/BlobCopyController.cs` (NEW)

**Dependencies**: TASK-B-004, TASK-B-003

---

#### TASK-B-008: Implement POST /api/v1/blob-copy/start Endpoint

**Category**: Backend API  
**Description**: Implement copy initiation endpoint. Validates inputs, starts async copy operation, returns immediately with operation ID and status (202 Accepted).

**Acceptance Criteria**:
- [ ] Controller: BlobCopyController.StartCopyAsync(CopyRequest)
- [ ] HTTP 202 response: BlobCopyOperation with id, status=pending, correlationId, timestamps
- [ ] HTTP 400 response: validation failed (includes errors array)
- [ ] HTTP 401 response: unauthorized
- [ ] HTTP 409 response: destination already exists (with guidance for user)
- [ ] HTTP 503 response: service unavailable
- [ ] Performs validation before starting copy
- [ ] Returns immediately (async copy happens in background)
- [ ] Generates unique operation ID and correlation ID
- [ ] Sets StartedAt timestamp

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Controllers/BlobCopyController.cs` (MODIFY)

**Dependencies**: TASK-B-005, TASK-B-007

---

#### TASK-B-009: Implement GET /api/v1/blob-copy/{id}/status Endpoint

**Category**: Backend API  
**Description**: Implement status polling endpoint. Returns current state of copy operation by ID.

**Acceptance Criteria**:
- [ ] Controller: BlobCopyController.GetStatusAsync(string id)
- [ ] HTTP 200 response: BlobCopyOperation (current state including progress)
- [ ] HTTP 404 response: operation not found
- [ ] HTTP 401 response: unauthorized
- [ ] Retrieves operation from in-memory store (or database)
- [ ] Includes all operation fields (status, progress, bytes, timestamps, error details)
- [ ] Response time ≤ 200ms p95

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Controllers/BlobCopyController.cs` (MODIFY)

**Dependencies**: TASK-B-005

---

#### TASK-B-010: Implement POST /api/v1/blob-copy/{id}/cancel Endpoint

**Category**: Backend API  
**Description**: Implement cancel endpoint. Requests cancellation of in-progress copy operation.

**Acceptance Criteria**:
- [ ] Controller: BlobCopyController.CancelAsync(string id)
- [ ] HTTP 200 response: BlobCopyOperation with status=cancelled
- [ ] HTTP 404 response: operation not found
- [ ] HTTP 409 response: operation cannot be cancelled (already completed/failed)
- [ ] HTTP 401 response: unauthorized
- [ ] Sets CancellationRequested = true on operation
- [ ] BlobCopyService respects cancellation and transitions to Cancelled state
- [ ] Returns updated operation state

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Controllers/BlobCopyController.cs` (MODIFY)

**Dependencies**: TASK-B-005

---

#### TASK-B-011: Implement GET /api/v1/health Endpoint

**Category**: Backend API  
**Description**: Implement health check endpoint for load balancers and monitoring.

**Acceptance Criteria**:
- [ ] Controller: HealthController.GetHealthAsync()
- [ ] HTTP 200 response: { status: "healthy" }
- [ ] HTTP 503 response: { status: "unhealthy" } (if dependency unreachable)
- [ ] Checks Azure Storage connectivity
- [ ] Checks SignalR hub status
- [ ] Simple, fast (< 1 second)
- [ ] No authentication required

**Effort**: 1 story point  
**Priority**: P1  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Controllers/HealthController.cs` (NEW)

**Dependencies**: TASK-B-003

---

---

### Backend Tests (xUnit, ≥80% Coverage)

---

#### TASK-B-012: Unit Tests for BlobValidationService

**Category**: Backend Tests  
**Description**: Comprehensive xUnit unit tests for BlobValidationService. Mock Azure SDK BlobClient. Test all validation rules and error conditions.

**Acceptance Criteria**:
- [ ] Test ValidateUriFormat: valid URIs pass, invalid URIs fail with INVALID_URI_FORMAT
- [ ] Test ValidateBlobExists: existing blob returns true, non-existent returns false
- [ ] Test ValidateDestinationWritable: writable destination returns true, read-only returns false
- [ ] Test ValidateIdenticalUris: returns error if source = destination
- [ ] Test ValidateRequest orchestration: all checks run, errors aggregated
- [ ] Test edge cases: empty fields, malformed URIs, special characters
- [ ] Test exception handling: Azure SDK exceptions mapped to ValidationError
- [ ] ≥85% code coverage for BlobValidationService
- [ ] All tests deterministic and fast (< 100ms each)

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.Tests/Unit/BlobValidationServiceTests.cs` (NEW)

**Dependencies**: TASK-B-004

---

#### TASK-B-013: Unit Tests for BlobCopyService

**Category**: Backend Tests  
**Description**: Comprehensive xUnit unit tests for BlobCopyService. Mock Azure SDK BlobClient and BlobProperties. Test copy flow, state transitions, progress calculation, error handling.

**Acceptance Criteria**:
- [ ] Test CopyBlobAsync: initiates copy, returns operation with ID
- [ ] Test state transitions: Pending → InProgress → Completed
- [ ] Test state transitions: Pending → InProgress → Failed (with error code)
- [ ] Test state transitions: InProgress → Cancelled
- [ ] Test progress calculation: bytes transferred, percentage, ETA
- [ ] Test cancellation: CancellationToken honored, operation transitions to Cancelled
- [ ] Test error handling: Azure SDK exceptions mapped to error codes
- [ ] Test edge cases: zero-byte blob, very large blob (mock), timeout scenarios
- [ ] Test correlation ID propagation through operation lifecycle
- [ ] ≥80% code coverage for BlobCopyService
- [ ] All tests deterministic and fast (< 200ms each)

**Effort**: 4 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.Tests/Unit/BlobCopyServiceTests.cs` (NEW)

**Dependencies**: TASK-B-005

---

#### TASK-B-014: Unit Tests for ProgressNotificationService

**Category**: Backend Tests  
**Description**: xUnit unit tests for ProgressNotificationService. Mock SignalR IHubContext. Test progress publishing and calculation.

**Acceptance Criteria**:
- [ ] Test PublishProgressAsync: sends ProgressUpdate to clients
- [ ] Test transfer rate calculation: MB/s computed correctly
- [ ] Test ETA calculation: estimated seconds remaining accurate
- [ ] Test exception handling: SignalR failures logged, service continues
- [ ] Test broadcasting to specific operation ID
- [ ] ≥85% code coverage for ProgressNotificationService
- [ ] All tests < 100ms each

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.Tests/Unit/ProgressNotificationServiceTests.cs` (NEW)

**Dependencies**: TASK-B-006

---

#### TASK-B-015: Integration Tests for BlobCopyController

**Category**: Backend Tests  
**Description**: xUnit integration tests for all 5 API endpoints. Use in-memory WebApplicationFactory for full HTTP testing. Mock Azure Blob Storage.

**Acceptance Criteria**:
- [ ] Test POST /validate: valid URIs return 200 with valid=true
- [ ] Test POST /validate: invalid URIs return 200 with valid=false + errors
- [ ] Test POST /validate: missing fields return 400
- [ ] Test POST /validate: unauthorized returns 401
- [ ] Test POST /start: valid request returns 202 with BlobCopyOperation
- [ ] Test POST /start: invalid URIs return 400 with error details
- [ ] Test POST /start: starts async copy operation
- [ ] Test GET /status/{id}: returns current operation state
- [ ] Test GET /status/{id}: non-existent ID returns 404
- [ ] Test POST /{id}/cancel: cancels in-progress operation
- [ ] Test POST /{id}/cancel: completed operation returns 409
- [ ] Test GET /health: returns 200 with healthy status
- [ ] All endpoints return proper HTTP status codes
- [ ] CORS headers present in responses
- [ ] ≥80% code coverage for BlobCopyController

**Effort**: 4 story points  
**Priority**: P0  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.Tests/Integration/BlobCopyControllerIntegrationTests.cs` (NEW)

**Dependencies**: TASK-B-007, TASK-B-008, TASK-B-009, TASK-B-010, TASK-B-011

---

#### TASK-B-016: API Contract Validation Tests

**Category**: Backend Tests  
**Description**: xUnit tests that validate API responses match OpenAPI contract. Use JSON schema validation.

**Acceptance Criteria**:
- [ ] POST /validate response matches ValidateResponse schema
- [ ] POST /start response matches BlobCopyOperation schema (202)
- [ ] GET /status response matches BlobCopyOperation schema
- [ ] POST /{id}/cancel response matches BlobCopyOperation schema
- [ ] GET /health response matches health schema
- [ ] Error responses match ErrorResponse schema
- [ ] All timestamp fields are ISO 8601 format
- [ ] All UUID fields are valid v4 format
- [ ] All tests pass on CI

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: Backend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.Tests/Unit/ApiContractTests.cs` (NEW)
- `src/backend/BlobCopy.Tests/contract.json` (reference OpenAPI schema)

**Dependencies**: TASK-B-007, TASK-B-008, TASK-B-009, TASK-B-010, TASK-B-011

---

---

### Frontend Components (React, TypeScript)

---

#### TASK-F-001: Implement BlobCopyForm Component

**Category**: Frontend Components  
**Description**: React component for input form. Two text fields for source and destination URIs, copy button, and real-time client-side validation.

**Acceptance Criteria**:
- [ ] Renders two labeled input fields (sourceUri, destinationUri)
- [ ] Placeholder text indicates URI format expected
- [ ] Copy button disabled until both URIs are non-empty
- [ ] Client-side validation on blur: checks URI format, shows error below field
- [ ] Shows error icon next to invalid field
- [ ] Copy button calls onSubmit callback with validated URIs
- [ ] Disables form and shows loading state during submit
- [ ] No server calls (validation is frontend-only format check)
- [ ] TypeScript with proper interface types
- [ ] Accessible: proper labels, ARIA attributes, keyboard navigation

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/BlobCopyForm.tsx` (NEW)
- `src/frontend/BlobCopy.UI/src/types/index.ts` (NEW - define form types)

**Dependencies**: None

---

#### TASK-F-002: Implement ProgressDisplay Component

**Category**: Frontend Components  
**Description**: React component displaying real-time copy progress. Progress bar, percentage, bytes transferred, transfer rate, and ETA.

**Acceptance Criteria**:
- [ ] Displays progress bar (0-100%) with animated fill
- [ ] Shows percentage text (e.g., "45%")
- [ ] Displays bytes transferred and total bytes (e.g., "50 MB / 100 MB")
- [ ] Shows transfer rate in MB/s (e.g., "23.5 MB/s")
- [ ] Shows estimated time remaining (e.g., "2 min 30 sec" or "45 seconds")
- [ ] Updates smoothly as progress updates arrive from SignalR
- [ ] Handles transition from in-progress to completed smoothly
- [ ] Accessible: proper ARIA labels for progress bar
- [ ] TypeScript with ProgressUpdate interface

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/ProgressDisplay.tsx` (NEW)

**Dependencies**: None

---

#### TASK-F-003: Implement ResultMessage Component

**Category**: Frontend Components  
**Description**: React component for displaying copy result (success or error). Shows message, details, and action buttons.

**Acceptance Criteria**:
- [ ] Success state: Shows checkmark icon, "Copy successful" message, destination URI, action buttons (Copy Another, Close)
- [ ] Error state: Shows error icon, error message, error code, action buttons (Retry, Copy Another)
- [ ] Different styling for success vs. error states (colors, icons)
- [ ] Message text is user-friendly and actionable
- [ ] Copy Another button clears form and focuses on source URI field
- [ ] Retry button re-initiates copy with same URIs
- [ ] Close button returns to initial state
- [ ] TypeScript with proper interfaces

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/ResultMessage.tsx` (NEW)

**Dependencies**: None

---

#### TASK-F-004: Implement CopyButton Component

**Category**: Frontend Components  
**Description**: Reusable button component for copy action. Shows loading state with spinner, disabled state, and tooltip.

**Acceptance Criteria**:
- [ ] Displays button with "Copy Blob" text
- [ ] Shows spinner icon when isLoading=true
- [ ] Disabled when disabled prop or loading
- [ ] Disables click handling during loading
- [ ] Optional tooltip on hover
- [ ] TypeScript with proper interface

**Effort**: 1 story point  
**Priority**: P1  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/CopyButton.tsx` (NEW)

**Dependencies**: None

---

---

### Frontend Services (Hooks, API, SignalR)

---

#### TASK-F-005: Implement useBlobCopy Custom Hook

**Category**: Frontend Services  
**Description**: Custom React hook managing blob copy operation state. Orchestrates API calls and SignalR updates.

**Acceptance Criteria**:
- [ ] Hook signature: useBlobCopy() returns { operation, isLoading, error, validate, startCopy, cancel, reset }
- [ ] operation: BlobCopyOperation state
- [ ] isLoading: boolean indicating pending request
- [ ] error: error state (null or error object)
- [ ] validate(sourceUri, destinationUri): calls API /validate endpoint
- [ ] startCopy(sourceUri, destinationUri): calls API /start endpoint
- [ ] cancel(operationId): calls API /{id}/cancel endpoint
- [ ] reset(): clears state, prepares for new copy
- [ ] Subscribes to SignalR progress updates on copy start
- [ ] Unsubscribes on cleanup or completion
- [ ] Uses useEffect for side effects
- [ ] TypeScript with proper types

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/hooks/useBlobCopy.ts` (NEW)

**Dependencies**: TASK-F-006, TASK-F-007

---

#### TASK-F-006: Implement useSignalR Custom Hook

**Category**: Frontend Services  
**Description**: Custom React hook managing SignalR WebSocket connection for real-time progress updates.

**Acceptance Criteria**:
- [ ] Hook signature: useSignalR(hubUrl) returns { onProgressUpdate, onCopyCompleted, onCopyFailed, isConnected }
- [ ] Establishes WebSocket connection to SignalR hub on mount
- [ ] onProgressUpdate(callback): registers handler for progress updates
- [ ] onCopyCompleted(callback): registers handler for completion
- [ ] onCopyFailed(callback): registers handler for errors
- [ ] isConnected: boolean indicating connection status
- [ ] Automatically reconnects on disconnect with exponential backoff
- [ ] Cleans up connection on unmount
- [ ] Handles connection failures gracefully
- [ ] TypeScript with proper types

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/hooks/useSignalR.ts` (NEW)

**Dependencies**: None

---

#### TASK-F-007: Implement blobCopyApiService

**Category**: Frontend Services  
**Description**: HTTP client service for API calls. Wraps axios with proper error handling, auth headers, and retry logic.

**Acceptance Criteria**:
- [ ] Service exports: validateUris(sourceUri, destinationUri), startCopy(sourceUri, destinationUri), getStatus(operationId), cancelCopy(operationId)
- [ ] All methods return Promise<T> with proper type signatures
- [ ] Includes Authorization header with Entra ID token (from MSAL)
- [ ] Implements exponential backoff retry on transient errors (429, 503, network timeouts)
- [ ] Maps API errors to user-friendly error messages
- [ ] Timeout set to 30 seconds
- [ ] Base URL from environment variable API_BASE_URL
- [ ] TypeScript with proper types

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/services/blobCopyApiService.ts` (NEW)

**Dependencies**: None

---

#### TASK-F-008: Implement signalRService

**Category**: Frontend Services  
**Description**: Low-level SignalR service wrapper. Manages connection, registration of handlers, and cleanup.

**Acceptance Criteria**:
- [ ] Service exports: connect(hubUrl), disconnect(), on(eventName, handler), off(eventName), isConnected
- [ ] Uses @microsoft/signalr package
- [ ] Handles connection state changes (connected, disconnected, reconnecting)
- [ ] Implements automatic reconnect with exponential backoff
- [ ] Maximum 5 reconnect attempts before giving up
- [ ] Logs connection events to console (development mode)
- [ ] Proper cleanup on disconnect
- [ ] TypeScript with proper types

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/services/signalRService.ts` (NEW)

**Dependencies**: None

---

#### TASK-F-009: Implement uriValidationService

**Category**: Frontend Services  
**Description**: Client-side URI validation utility. Validates format before sending to API.

**Acceptance Criteria**:
- [ ] Function signature: validateUri(uri: string): { valid: boolean, error?: string }
- [ ] Regex pattern: `https://[a-z0-9]+\.blob\.core\.windows\.net/[a-z0-9-]+/.*`
- [ ] Checks for HTTPS protocol
- [ ] Checks for valid Azure domain
- [ ] Checks container and blob name format
- [ ] Returns specific error messages for each validation rule
- [ ] Function: checkIdenticalUris(sourceUri, destinationUri): boolean
- [ ] TypeScript with proper types

**Effort**: 1 story point  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/services/uriValidationService.ts` (NEW)

**Dependencies**: None

---

---

### Frontend Tests (Vitest, ≥80% Coverage)

---

#### TASK-F-010: Unit Tests for BlobCopyForm Component

**Category**: Frontend Tests  
**Description**: Vitest unit tests for BlobCopyForm component. Mock onSubmit callback, test form interactions and validation.

**Acceptance Criteria**:
- [ ] Test render: inputs and button visible
- [ ] Test initial state: button disabled
- [ ] Test enable button: when both URIs non-empty, button enabled
- [ ] Test validation error display: invalid URI shows error below field
- [ ] Test validation on blur: triggered after field loses focus
- [ ] Test onSubmit callback: called with URIs on copy button click
- [ ] Test form submission disabled during loading state
- [ ] Test accessibility: labels associated with inputs, button accessible
- [ ] ≥85% code coverage for component logic

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/__tests__/BlobCopyForm.test.tsx` (NEW)

**Dependencies**: TASK-F-001

---

#### TASK-F-011: Unit Tests for ProgressDisplay Component

**Category**: Frontend Tests  
**Description**: Vitest unit tests for ProgressDisplay component. Test progress bar updates, percentage display, ETA formatting.

**Acceptance Criteria**:
- [ ] Test render: progress bar, percentage, bytes, rate, ETA all visible
- [ ] Test progress update: progress bar width updates correctly
- [ ] Test percentage display: formatted correctly (e.g., "45.5%")
- [ ] Test bytes display: formatted with units (e.g., "50 MB / 100 MB")
- [ ] Test ETA formatting: seconds < 60 show as "45s", ≥ 60 show as "2m 30s"
- [ ] Test rate calculation: MB/s displayed with 1 decimal place
- [ ] Test animation: progress bar transitions smoothly
- [ ] ≥85% code coverage

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/__tests__/ProgressDisplay.test.tsx` (NEW)

**Dependencies**: TASK-F-002

---

#### TASK-F-012: Unit Tests for ResultMessage Component

**Category**: Frontend Tests  
**Description**: Vitest unit tests for ResultMessage component. Test success/error states, action button callbacks.

**Acceptance Criteria**:
- [ ] Test success state: shows checkmark, success message, destination URI
- [ ] Test error state: shows error icon, error message, error code
- [ ] Test Copy Another button: calls onCopyAgain callback
- [ ] Test Retry button: calls onRetry callback
- [ ] Test Close button: calls onClose callback
- [ ] Test different styling: success and error have distinct visual styles
- [ ] Test accessibility: proper heading hierarchy, button labels
- [ ] ≥85% code coverage

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/components/__tests__/ResultMessage.test.tsx` (NEW)

**Dependencies**: TASK-F-003

---

#### TASK-F-013: Unit Tests for useBlobCopy Hook

**Category**: Frontend Tests  
**Description**: Vitest unit tests for useBlobCopy hook. Mock API service and SignalR hook. Test state transitions and callbacks.

**Acceptance Criteria**:
- [ ] Test validate method: calls API service
- [ ] Test startCopy method: calls API service, returns operation ID
- [ ] Test cancel method: calls API service with operation ID
- [ ] Test reset method: clears state
- [ ] Test error handling: API errors mapped to error state
- [ ] Test loading state transitions
- [ ] Test integration with SignalR: subscribes on start, unsubscribes on cleanup
- [ ] Test cleanup: unsubscribes from SignalR on unmount
- [ ] ≥85% code coverage

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/hooks/__tests__/useBlobCopy.test.ts` (NEW)

**Dependencies**: TASK-F-005, TASK-F-006, TASK-F-007

---

#### TASK-F-014: Unit Tests for useSignalR Hook

**Category**: Frontend Tests  
**Description**: Vitest unit tests for useSignalR hook. Mock SignalR service. Test connection, event registration, cleanup.

**Acceptance Criteria**:
- [ ] Test connect: establishes connection on mount
- [ ] Test onProgressUpdate: registers handler and receives updates
- [ ] Test onCopyCompleted: registers handler
- [ ] Test onCopyFailed: registers handler
- [ ] Test isConnected: reflects connection status
- [ ] Test cleanup: disconnects on unmount
- [ ] Test reconnection: automatic reconnect on disconnect
- [ ] Test error handling: graceful failure on connection error
- [ ] ≥85% code coverage

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/hooks/__tests__/useSignalR.test.ts` (NEW)

**Dependencies**: TASK-F-006

---

#### TASK-F-015: Unit Tests for blobCopyApiService

**Category**: Frontend Tests  
**Description**: Vitest unit tests for blobCopyApiService. Mock axios. Test API calls, error handling, retries.

**Acceptance Criteria**:
- [ ] Test validateUris: calls POST /validate with correct payload
- [ ] Test startCopy: calls POST /start with correct payload
- [ ] Test getStatus: calls GET /status/{id}
- [ ] Test cancelCopy: calls POST /{id}/cancel
- [ ] Test error handling: maps error codes to user messages
- [ ] Test retry logic: retries on 429, 503, network timeouts
- [ ] Test Authorization header: included with token
- [ ] Test timeout: set to 30 seconds
- [ ] ≥85% code coverage

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/services/__tests__/blobCopyApiService.test.ts` (NEW)

**Dependencies**: TASK-F-007

---

#### TASK-F-016: Unit Tests for uriValidationService

**Category**: Frontend Tests  
**Description**: Vitest unit tests for uriValidationService. Test URI format validation, error messages, identical URI check.

**Acceptance Criteria**:
- [ ] Test valid URI: returns { valid: true }
- [ ] Test invalid protocol: returns error about HTTPS
- [ ] Test invalid domain: returns error about blob.core.windows.net
- [ ] Test invalid container name: returns error about container format
- [ ] Test identical URIs: checkIdenticalUris returns true
- [ ] Test different URIs: checkIdenticalUris returns false
- [ ] Test edge cases: empty string, null, special characters
- [ ] ≥90% code coverage

**Effort**: 1 story point  
**Priority**: P0  
**Owner**: Frontend  
**Files to Create/Modify**:
- `src/frontend/BlobCopy.UI/src/services/__tests__/uriValidationService.test.ts` (NEW)

**Dependencies**: TASK-F-009

---

---

### E2E Tests (Playwright, Cross-Browser)

---

#### TASK-E-001: Playwright Setup & Configuration

**Category**: E2E Tests  
**Description**: Configure Playwright with cross-browser support (Chrome, Firefox, Safari), authentication setup, and test utilities.

**Acceptance Criteria**:
- [ ] playwright.config.ts: configured for Chrome, Firefox, Safari
- [ ] Base URL points to local dev server (http://localhost:3000)
- [ ] API base URL from environment variable
- [ ] Headless mode for CI, headed mode for local debugging
- [ ] Screenshots and videos captured on failure
- [ ] Retry flaky tests (1 retry)
- [ ] Test timeout: 30 seconds per test
- [ ] Auth setup script: authenticates with Entra ID (or mocks auth token for testing)
- [ ] Shared fixtures for API client, storage operations

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/playwright.config.ts` (NEW)
- `tests/e2e/fixtures/auth.setup.ts` (NEW)
- `tests/e2e/fixtures/storageFixture.ts` (NEW)

**Dependencies**: None

---

#### TASK-E-002: E2E Test – User Story 1 (Copy Blob with Progress)

**Category**: E2E Tests  
**Description**: Playwright E2E test for User Story 1: Copy blob from source to destination with progress tracking.

**Acceptance Criteria**:
- [ ] Test flow:
  1. User navigates to app
  2. User enters valid source blob URI
  3. User enters valid destination blob URI
  4. User clicks Copy button
  5. Progress bar appears and updates in real-time
  6. Progress updates arrive via SignalR (verify in network tab or mock)
  7. Progress reaches 100%
  8. Success message displayed with destination URI
  9. "Copy Another" button available
  10. Destination blob verified via API call (blob properties match source)
- [ ] Test runs on Chrome, Firefox, Safari
- [ ] Test uses real or mocked Azure blobs (< 1 MB for speed)
- [ ] Assertions on DOM elements (progress bar width, percentage text, success message)
- [ ] Verifies destination blob via API call

**Effort**: 4 story points  
**Priority**: P0  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/e2e/blob-copy-success.spec.ts` (NEW)

**Dependencies**: TASK-E-001

---

#### TASK-E-003: E2E Test – User Story 2 (Handle Failures)

**Category**: E2E Tests  
**Description**: Playwright E2E test for User Story 2: Handle copy failures gracefully with error messages.

**Acceptance Criteria**:
- [ ] Test invalid source URI: error message shown
- [ ] Test non-existent source blob: error message shown
- [ ] Test insufficient permissions: error message shown
- [ ] Test invalid destination URI: error message shown
- [ ] Test network error (simulate): error message shown, retry available
- [ ] Each test verifies error message is user-friendly and actionable
- [ ] Retry button re-attempts copy with same URIs
- [ ] Tests run on Chrome, Firefox, Safari

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/e2e/blob-copy-errors.spec.ts` (NEW)

**Dependencies**: TASK-E-001

---

#### TASK-E-004: E2E Test – User Story 3 (Input Validation)

**Category**: E2E Tests  
**Description**: Playwright E2E test for User Story 3: Input validation on form.

**Acceptance Criteria**:
- [ ] Test empty fields: validation error shown, copy button disabled
- [ ] Test invalid URI format: validation error shown on blur
- [ ] Test identical URIs: validation error shown
- [ ] Test valid URIs: no validation error, copy button enabled
- [ ] Test client-side validation before API call (verify API not called for invalid input)
- [ ] Tests run on Chrome, Firefox, Safari

**Effort**: 2 story points  
**Priority**: P0  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/e2e/blob-copy-validation.spec.ts` (NEW)

**Dependencies**: TASK-E-001

---

#### TASK-E-005: E2E Test – Accessibility (WCAG AA)

**Category**: E2E Tests  
**Description**: Playwright E2E test for accessibility compliance. Verify WCAG AA baseline on form, progress, and result screens.

**Acceptance Criteria**:
- [ ] Form inputs have proper labels and ARIA attributes
- [ ] Progress bar has ARIA role and description
- [ ] Buttons are keyboard accessible (Tab, Enter)
- [ ] Focus management: focus moves logically through form
- [ ] Color contrast meets WCAG AA (4.5:1 for normal text)
- [ ] Error messages announced to screen readers
- [ ] Test using axe-core or similar accessibility checker
- [ ] Tests run on Chrome, Firefox

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/e2e/blob-copy-accessibility.spec.ts` (NEW)

**Dependencies**: TASK-E-001

---

#### TASK-E-006: E2E Test – Cancel Operation

**Category**: E2E Tests  
**Description**: Playwright E2E test for cancel functionality. User cancels an in-progress copy.

**Acceptance Criteria**:
- [ ] Test flow:
  1. User initiates copy of large blob (mocked to take > 10 seconds)
  2. Progress bar shows 20-30%
  3. User clicks cancel button (if available in UI)
  4. Copy operation cancelled
  5. Status changes to "cancelled"
  6. Error message or cancellation confirmation shown
- [ ] Or test via API call to /{id}/cancel endpoint

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/e2e/blob-copy-cancel.spec.ts` (NEW)

**Dependencies**: TASK-E-001

---

#### TASK-E-007: E2E Test – Health Check Endpoint

**Category**: E2E Tests  
**Description**: Playwright E2E test verifying health check endpoint responds with 200.

**Acceptance Criteria**:
- [ ] Test GET /api/v1/health returns 200 with { status: "healthy" }
- [ ] Test health check is called periodically by monitoring

**Effort**: 1 story point  
**Priority**: P1  
**Owner**: QA  
**Files to Create/Modify**:
- `tests/e2e/api-health.spec.ts` (NEW)

**Dependencies**: TASK-E-001

---

---

### CI/CD & Documentation

---

#### TASK-D-001: Create OpenAPI Documentation

**Category**: CI/CD & Documentation  
**Description**: Generate OpenAPI 3.0.0 specification from API contracts. Publish to Swagger UI endpoint.

**Acceptance Criteria**:
- [ ] Create api.openapi.yaml with full OpenAPI 3.0.0 spec (from api.contracts.md)
- [ ] Include all 5 endpoints (validate, start, status, cancel, health)
- [ ] Include all request/response schemas
- [ ] Include security schemes (Bearer token)
- [ ] Publish to GET /swagger/index.html or similar
- [ ] Swagger UI accessible and interactive
- [ ] Download OpenAPI spec available

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: Backend  
**Files to Create/Modify**:
- `specs/001-blob-copy/contracts/api.openapi.yaml` (NEW - if not in contracts)
- `src/backend/BlobCopy.API/OpenAPI/swagger.json` (generated)

**Dependencies**: TASK-B-007, TASK-B-008, TASK-B-009, TASK-B-010

---

#### TASK-D-002: GitHub Actions CI/CD Pipeline

**Category**: CI/CD & Documentation  
**Description**: Create GitHub Actions workflow for build, test, and deploy. Runs on PR and merge to main.

**Acceptance Criteria**:
- [ ] Trigger: on PR and merge to main
- [ ] Job 1: Backend Build & Test
  - Restore NuGet packages
  - Build backend
  - Run xUnit tests
  - Measure coverage (≥80%)
  - Report coverage to Codecov or GitHub
- [ ] Job 2: Frontend Build & Test
  - Install npm packages
  - Build frontend
  - Run Vitest tests
  - Measure coverage (≥80%)
- [ ] Job 3: E2E Tests (on main branch only)
  - Run Playwright tests on Chrome, Firefox, Safari
  - Upload screenshots/videos on failure
- [ ] Fail workflow if coverage < 80%
- [ ] Fail workflow if tests don't pass
- [ ] Generate test report artifacts

**Effort**: 3 story points  
**Priority**: P0  
**Owner**: Backend/Frontend  
**Files to Create/Modify**:
- `.github/workflows/build-test-deploy.yml` (NEW)
- `.github/workflows/e2e-tests.yml` (NEW)

**Dependencies**: TASK-B-007-016, TASK-F-001-016, TASK-E-001-007

---

#### TASK-D-003: Local Development Setup Guide

**Category**: CI/CD & Documentation  
**Description**: Create comprehensive quickstart.md for local development setup.

**Acceptance Criteria**:
- [ ] Prerequisites (.NET 10 SDK, Node.js, Docker Desktop)
- [ ] Clone repo and cd to project
- [ ] Backend setup:
  - Restore NuGet packages
  - Set up Azure Storage Emulator or connection string
  - dotnet build
  - dotnet run (starts backend on http://localhost:5000)
- [ ] Frontend setup:
  - npm install
  - npm run dev (starts dev server on http://localhost:3000)
- [ ] Test backend:
  - dotnet test
  - Verify ≥80% coverage
- [ ] Test frontend:
  - npm run test
  - Verify ≥80% coverage
- [ ] Run E2E tests locally:
  - npx playwright install
  - npx playwright test
- [ ] Access Swagger UI: http://localhost:5000/swagger/index.html
- [ ] Troubleshooting section

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: Backend/Frontend  
**Files to Create/Modify**:
- `specs/001-blob-copy/quickstart.md` (MODIFY or NEW)
- `README.md` (MODIFY to include link to quickstart)

**Dependencies**: All implementation tasks

---

#### TASK-D-004: Application Insights Integration

**Category**: CI/CD & Documentation  
**Description**: Integrate Azure Application Insights for monitoring, logging, and distributed tracing.

**Acceptance Criteria**:
- [ ] Backend: Add Application Insights NuGet package
- [ ] Log all API requests with status, latency, correlation ID
- [ ] Track exceptions with full stack traces
- [ ] Track custom events: copy initiated, progress milestone, copy completed
- [ ] Distributed tracing: correlation ID propagates through all logs
- [ ] Frontend: Add Application Insights JavaScript SDK
- [ ] Log page views and user actions
- [ ] Track custom events: form submitted, copy started, copy completed
- [ ] Configure Application Insights connection string from environment variable
- [ ] Dashboard in Azure portal showing key metrics

**Effort**: 2 story points  
**Priority**: P1  
**Owner**: Backend/Frontend  
**Files to Create/Modify**:
- `src/backend/BlobCopy.API/Startup.cs` (MODIFY - add App Insights)
- `src/frontend/BlobCopy.UI/src/services/appInsightsService.ts` (NEW)
- Documentation on App Insights setup

**Dependencies**: All implementation tasks

---

#### TASK-D-005: Deployment & Infrastructure Docs

**Category**: CI/CD & Documentation  
**Description**: Document deployment architecture and create Bicep/Terraform templates for Azure infrastructure.

**Acceptance Criteria**:
- [ ] Document architecture: App Service (backend) + Static Web Apps (frontend) + Blob Storage
- [ ] Bicep templates for:
  - App Service plan and instance
  - Static Web Apps instance
  - Application Insights
  - Key Vault for secrets
  - Managed identity for backend
- [ ] Or Terraform equivalents
- [ ] README on how to deploy to Azure
- [ ] Environment variables and configuration documentation
- [ ] Scaling and monitoring recommendations

**Effort**: 3 story points  
**Priority**: P2  
**Owner**: Infrastructure  
**Files to Create/Modify**:
- `infra/bicep/main.bicep` (NEW) or `infra/terraform/main.tf` (NEW)
- `docs/DEPLOYMENT.md` (NEW)

**Dependencies**: All implementation tasks

---

---

## Recommended Implementation Sequence

### Phase 2A: Foundation (Weeks 1-2)

1. **TASK-B-001**: Create Core Data Models
2. **TASK-F-009**: Implement uriValidationService
3. **TASK-B-003**: Setup Dependency Injection & Configuration
4. **TASK-B-002**: Configure SignalR Hub
5. **TASK-F-004**: Implement CopyButton Component

### Phase 2B: Backend Services (Weeks 2-3)

6. **TASK-B-004**: Implement BlobValidationService
7. **TASK-B-005**: Implement BlobCopyService
8. **TASK-B-006**: Implement ProgressNotificationService

### Phase 2C: Backend API (Week 3)

9. **TASK-B-007**: Implement POST /validate Endpoint
10. **TASK-B-008**: Implement POST /start Endpoint
11. **TASK-B-009**: Implement GET /status Endpoint
12. **TASK-B-010**: Implement POST /cancel Endpoint
13. **TASK-B-011**: Implement GET /health Endpoint

### Phase 2D: Backend Tests (Weeks 3-4)

14. **TASK-B-012**: Unit Tests for BlobValidationService
15. **TASK-B-013**: Unit Tests for BlobCopyService
16. **TASK-B-014**: Unit Tests for ProgressNotificationService
17. **TASK-B-015**: Integration Tests for BlobCopyController
18. **TASK-B-016**: API Contract Validation Tests

### Phase 2E: Frontend Services (Weeks 2-3)

19. **TASK-F-007**: Implement blobCopyApiService
20. **TASK-F-008**: Implement signalRService
21. **TASK-F-006**: Implement useSignalR Hook
22. **TASK-F-005**: Implement useBlobCopy Hook

### Phase 2F: Frontend Components (Week 4)

23. **TASK-F-001**: Implement BlobCopyForm Component
24. **TASK-F-002**: Implement ProgressDisplay Component
25. **TASK-F-003**: Implement ResultMessage Component

### Phase 2G: Frontend Tests (Week 4)

26. **TASK-F-010**: Unit Tests for BlobCopyForm
27. **TASK-F-011**: Unit Tests for ProgressDisplay
28. **TASK-F-012**: Unit Tests for ResultMessage
29. **TASK-F-013**: Unit Tests for useBlobCopy Hook
30. **TASK-F-014**: Unit Tests for useSignalR Hook
31. **TASK-F-015**: Unit Tests for blobCopyApiService
32. **TASK-F-016**: Unit Tests for uriValidationService

### Phase 2H: E2E Tests (Weeks 4-5)

33. **TASK-E-001**: Playwright Setup & Configuration
34. **TASK-E-002**: E2E Test – User Story 1 (Copy Success)
35. **TASK-E-003**: E2E Test – User Story 2 (Handle Failures)
36. **TASK-E-004**: E2E Test – User Story 3 (Input Validation)
37. **TASK-E-005**: E2E Test – Accessibility (WCAG AA)
38. **TASK-E-006**: E2E Test – Cancel Operation
39. **TASK-E-007**: E2E Test – Health Check

### Phase 2I: CI/CD & Documentation (Week 5)

40. **TASK-D-001**: Create OpenAPI Documentation
41. **TASK-D-002**: GitHub Actions CI/CD Pipeline
42. **TASK-D-003**: Local Development Setup Guide
43. **TASK-D-004**: Application Insights Integration
44. **TASK-D-005**: Deployment & Infrastructure Docs

---

## Summary Metrics

### Task Count by Category

| Category | Task Count | Story Points |
|----------|-----------|--------------|
| Backend Infrastructure | 3 | 7 |
| Backend Services | 3 | 10 |
| Backend API | 5 | 11 |
| Backend Tests | 5 | 15 |
| Frontend Components | 4 | 8 |
| Frontend Services | 5 | 13 |
| Frontend Tests | 7 | 14 |
| E2E Tests | 7 | 16 |
| CI/CD & Documentation | 5 | 12 |
| **TOTAL** | **44** | **106** |

### Effort by Owner

| Owner | Tasks | Story Points | Estimate (days @ 8 pts/day) |
|-------|-------|------------|---------------------------|
| Backend | 18 | 43 | 5.4 days |
| Frontend | 16 | 35 | 4.4 days |
| QA/E2E | 7 | 16 | 2 days |
| Infrastructure | 3 | 12 | 1.5 days |

### Coverage Targets

- **Backend Services**: ≥80% code coverage (xUnit)
- **Backend Controllers**: ≥80% code coverage (integration tests)
- **Frontend Components**: ≥85% code coverage (Vitest)
- **Frontend Hooks**: ≥85% code coverage (Vitest)
- **Frontend Services**: ≥85% code coverage (Vitest)
- **E2E Test Coverage**: All 3 user stories + accessibility + cancel + health

### Success Criteria

✅ All 44 tasks completed  
✅ ≥80% test coverage on backend services  
✅ ≥80% test coverage on frontend components/hooks/services  
✅ All 5 API endpoints operational (validate, start, status, cancel, health)  
✅ Real-time progress updates via SignalR ≥ every 5 seconds  
✅ E2E tests passing on Chrome, Firefox, Safari  
✅ API response latency ≤ 200ms p95 (validation endpoint)  
✅ All PRs pass Copilot automated code review  
✅ All PRs have human code review approval  
✅ OpenAPI documentation generated and accessible  
✅ CI/CD pipeline automated and gating merges  

---

**End of Phase 2 Task Breakdown**

_This document is ready for handoff to development teams. Assign tasks to individual contributors, track progress in your project management tool, and update this document as tasks are completed._

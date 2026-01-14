# Implementation Plan: Blob Storage Copy

**Branch**: `001-blob-copy` | **Date**: January 4, 2026 | **Spec**: [spec.md](spec.md)
**Input**: Feature specification from `/specs/001-blob-copy/spec.md`

**Note**: This template is filled in by the `/speckit.plan` command. See `.specify/templates/commands/plan.md` for the execution workflow.

## Summary

Build a .NET 10 (C#) backend service with a React.js frontend that allows users to copy Azure Blob Storage blobs from a source URI to a destination URI. The system will display real-time progress updates using SignalR WebSockets and provide clear success/failure feedback. The backend will use Azure SDK for .NET (Azure.Storage.Blobs) for blob operations and Azure Entra ID for authentication. The frontend will provide a responsive UI with input validation, progress bars, and actionable error messages.

## Technical Context

**Language/Version**: C# with .NET 10 (backend), TypeScript with React.js 18+ (frontend)
**Primary Dependencies**: 
  - Backend: Azure.Storage.Blobs, Azure.Identity, SignalR Core
  - Frontend: React, TypeScript, Axios, SignalR Client
  - Testing: Playwright (integration/E2E), Vitest (unit tests)
**Storage**: Azure Blob Storage (managed service, no local storage)
**Testing**: xUnit (backend unit/integration tests), Vitest (frontend unit tests), Playwright (frontend/API integration and E2E tests)
**Target Platform**: Cloud-hosted (.NET backend on Azure App Service or Azure Functions, React SPA on Azure Static Web Apps)
**Project Type**: Web (backend API + frontend SPA)
**Performance Goals**: 
  - API response for validation: < 200ms (p95) per constitution
  - Progress updates: every 5 seconds or better
  - Support blobs up to multi-gigabyte size with streaming downloads
**Constraints**: 
  - API latency ≤ 200ms p95 for validation endpoints (per constitution)
  - Memory-efficient for large blob handling (streaming, not buffering entire blob)
  - Real-time progress via WebSocket to avoid polling overhead
**Scale/Scope**: MVP with 3 user stories, single copy operation at a time per session

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

### Applied Principles (from Speckit Constitution v1.0)

✅ **Code Quality** (Principle 1): 
- Clarity and single responsibility enforced through service/controller separation
- Consistent style: Azure SDK idioms + React best practices
- Code review mandatory (Copilot automated review on all PRs)

✅ **Technology Stack** (Principle 2):
- Backend: .NET 10 C# ✓ (constitution requirement)
- Frontend: React.js 18+ ✓ (constitution requirement for UI work)
- Azure integration for blob operations ✓

✅ **Testing Standards** (Principle 3):
- Unit tests: ≥80% coverage target for core BlobCopyService and validation logic
- Unit test framework: xUnit (backend), Vitest (frontend) per language standards
- Integration tests: Playwright for browser-based API integration testing, xUnit for backend service integration
- E2E tests: Playwright with cross-browser support (Chrome, Firefox, Safari) per constitution requirement
- UI tests: Playwright for user interaction flows; Vitest for isolated component logic
- CI gating: All tests must pass before merge

✅ **User Experience Consistency** (Principle 4):
- React.js for all UI ✓
- WCAG AA accessibility baseline (form labels, ARIA attributes, keyboard navigation)
- Actionable error messages with recovery guidance
- Real-time progress feedback (WebSocket-based)

✅ **Performance Requirements** (Principle 5):
- Validation endpoint latency target: < 200ms p95 ✓
- Streaming blob copy for memory efficiency
- Real-time progress via WebSocket to avoid polling overhead

✅ **Observability** (Principle 6):
- Azure Application Insights integration: track copy operations, errors, latency
- Distributed tracing: correlate API requests with blob operations
- Custom events: copy initiated, progress milestones, copy completed/failed

✅ **API Design** (Principle 7):
- REST endpoints for validation, copy initiation, status
- OpenAPI/Swagger documentation
- Proper HTTP status codes (400 validation error, 401 auth, 503 service unavailable)

✅ **Infrastructure & Cloud** (Principle 8):
- Azure Blob Storage for blob operations
- Infrastructure as Code: use Bicep/Terraform for App Service, Static Web Apps (not created here, documented for future deployment)

✅ **Resiliency** (Principle 9):
- Retry logic for transient blob storage failures (exponential backoff)
- Cancel operation support for long-running copies
- Circuit breaker pattern for external service calls

✅ **Application Security** (Principle 10):
- Azure Entra ID for authentication ✓
- Managed identity for backend → blob storage access
- No credentials in code; use Key Vault for secrets
- Audit logging of copy operations in Application Insights

**GATE RESULT**: ✅ **PASS** - All constitution principles satisfied. No violations requiring justification.

## Project Structure

### Documentation (this feature)

```text
specs/001-blob-copy/
├── spec.md              # Feature specification (user stories, requirements, success criteria)
├── plan.md              # This file (technical approach, architecture, design decisions)
├── research.md          # Phase 0: Clarification research (NEEDS CLARIFICATION items resolved)
├── data-model.md        # Phase 1: Data entities, state machines, validation rules
├── quickstart.md        # Phase 1: Quick start guide for running the feature locally
├── contracts/           # Phase 1: API contracts (OpenAPI/Swagger schemas)
│   ├── api.openapi.yaml
│   └── signalr.events.md
└── tasks.md             # Phase 2: Breakdown into actionable tasks (created by /speckit.tasks)
```

### Source Code (repository root)

```text
src/
├── backend/
│   ├── BlobCopy.API/                           # ASP.NET Core backend
│   │   ├── Controllers/
│   │   │   ├── BlobCopyController.cs            # HTTP endpoints: validate, initiate, status
│   │   │   └── HealthController.cs              # Health check endpoint
│   │   ├── Services/
│   │   │   ├── BlobCopyService.cs               # Core blob copy logic
│   │   │   ├── BlobValidationService.cs         # URI validation
│   │   │   └── ProgressNotificationService.cs   # WebSocket progress updates
│   │   ├── Models/
│   │   │   ├── CopyRequest.cs                   # Request DTO
│   │   │   ├── CopyResult.cs                    # Response DTO
│   │   │   └── ProgressUpdate.cs                # Progress event DTO
│   │   ├── Hubs/
│   │   │   └── BlobCopyHub.cs                   # SignalR hub for real-time progress
│   │   ├── Middleware/
│   │   │   └── ErrorHandlingMiddleware.cs       # Global error handling
│   │   ├── Program.cs                           # Startup/DI configuration
│   │   └── appsettings.json
│   │
│   └── BlobCopy.Tests/
│       ├── Unit/
│       │   ├── BlobValidationServiceTests.cs
│       │   ├── BlobCopyServiceTests.cs
│       │   └── ProgressNotificationServiceTests.cs
│       └── Integration/
│           ├── BlobCopyControllerIntegrationTests.cs
│           └── BlobCopyE2ETests.cs
│
├── frontend/
│   ├── BlobCopy.UI/                            # React TypeScript SPA
│   │   ├── src/
│   │   │   ├── components/
│   │   │   │   ├── BlobCopyForm.tsx             # Input form for URIs
│   │   │   │   ├── ProgressDisplay.tsx          # Real-time progress bar
│   │   │   │   ├── ResultMessage.tsx            # Success/error message display
│   │   │   │   └── CopyButton.tsx               # Copy action button
│   │   │   ├── services/
│   │   │   │   ├── blobCopyApiService.ts        # HTTP client for API calls
│   │   │   │   ├── signalRService.ts            # WebSocket client for progress
│   │   │   │   └── uriValidationService.ts      # Client-side validation
│   │   │   ├── hooks/
│   │   │   │   ├── useBlobCopy.ts               # Custom hook for copy operation
│   │   │   │   └── useSignalR.ts                # Custom hook for SignalR connection
│   │   │   ├── types/
│   │   │   │   └── index.ts                     # TypeScript interfaces
│   │   │   ├── App.tsx
│   │   │   └── index.tsx
│   │   ├── public/
│   │   └── package.json
│   │
│   └── BlobCopy.UI.Tests/
│       ├── unit/
│       │   ├── components/
│       │   │   ├── BlobCopyForm.test.tsx        # Component logic unit tests (Vitest)
│       │   │   ├── ProgressDisplay.test.tsx
│       │   │   └── ResultMessage.test.tsx
│       │   └── services/
│       │       ├── blobCopyApiService.test.ts
│       │       └── uriValidationService.test.ts
│       └── e2e/
│           ├── blob-copy-flow.spec.ts           # End-to-end user flow (Playwright)
│           ├── error-handling.spec.ts           # Error scenarios (Playwright)
│           └── accessibility.spec.ts            # WCAG AA accessibility (Playwright)
│
├── tests/
│   ├── contract/
│   │   ├── api.contract.test.ts                 # API schema validation tests (Playwright)
│   │   └── signalr.contract.test.ts             # SignalR event schema validation
│   ├── integration/
│   │   ├── backend-integration.test.cs          # Backend service integration (xUnit)
│   │   └── api-blob-storage.test.ts             # API to Blob Storage integration (Playwright)
│   └── playwright.config.ts                     # Playwright configuration
│       └── auth.setup.ts                        # Authentication setup for Playwright tests
    └── blob-copy-flow.e2e.test.ts               # End-to-end flow test
```

**Structure Decision**: Web application with separated backend API (.NET) and frontend SPA (React). This structure allows:
- Independent testing and deployment of backend and frontend
- Scalability: frontend served from static web apps, backend from app service
- Clear separation of concerns: backend handles Azure integration, frontend handles UX
- RESTful API contract between frontend and backend

## Complexity Tracking

No constitution violations requiring justification. Architecture aligns with all principles.

| Design Decision | Rationale | Simpler Alternative Rejected |
|-----------------|-----------|------------------------------|
| SignalR for progress updates | Reduces client polling overhead, enables real-time feedback per constitution | HTTP polling would violate performance budget and consume excessive API calls |
| Managed identity for blob access | Eliminates credentials from code, aligns with security principle | Connection string in appsettings would violate security principle 10 |
| Separate backend/frontend projects | Allows independent deployment and testing | Monolithic SPA would violate single responsibility principle |
| Azure Blob Storage API streaming | Handles large blobs without loading entire file in memory | Buffering entire blob would violate resource efficiency constraint |
| Azure API Management deferred (MVP exception) | MVP scope limits to direct App Service exposure; APIM adds complexity without immediate benefit for single-user scenario | Full APIM integration adds 2-3 days; defer to v1.1 when multi-tenant or rate limiting needed. Constitution Principle 7 exception documented per governance process. |

---

## Phase 0: Research & Clarification

### Resolved Clarifications (Session 2026-01-13)

All clarification questions have been resolved. The following decisions guide implementation:

1. **FR-009: Destination blob already exists behavior**
   - **Question**: When a blob already exists at the destination URI, what should the system do?
   - **Decision**: Prompt user to confirm overwrite, cancel, or rename
   - **Rationale**: Prevents accidental data loss; gives user control over conflict resolution
   - **Implementation**: 
     - Backend: Check for existing blob before copy operation
     - Frontend: Display modal dialog with three action buttons: "Overwrite", "Cancel", "Rename"
     - If rename selected, allow user to enter new destination blob name
     - Store user's choice in operation context for audit logging

2. **FR-010: Authentication method**
   - **Question**: How should users authenticate to access Azure Blob Storage?
   - **Decision**: Azure Entra ID with DefaultAzureCredential
   - **Rationale**: Aligns with constitution principle 10; no credentials in code; leverages Azure native auth; supports managed identity, Azure CLI, environment variables
   - **Implementation**: 
     - Backend: Use Azure.Identity.DefaultAzureCredential for all BlobServiceClient instances
     - Frontend: Use MSAL.js for user sign-in via Entra ID
     - No connection strings or access keys in configuration files
     - Document authentication setup in quickstart.md

3. **FR-012: Maximum blob size support**
   - **Question**: What is the maximum blob size the system should support?
   - **Decision**: 5 TB (Azure block blob limit)
   - **Rationale**: Aligns with Azure Blob Storage block blob maximum size limit; Azure SDK handles chunking automatically
   - **Implementation**:
     - Use Azure SDK's built-in chunking and retry mechanisms
     - Configure BlobUploadOptions with optimal chunk size (100 MB recommended)
     - Progress tracking reports percentage based on total blob size
     - No client-side size validation needed (Azure enforces limit)
     - Document large file handling in data-model.md

4. **FR-013: Copy operation timeout handling**
   - **Question**: How should the system handle copy operation timeouts?
   - **Decision**: Notify user and offer retry option
   - **Rationale**: Long-running operations may timeout due to network issues or service limits; user should have control to retry
   - **Implementation**: 
     - Set blob copy timeout to 30 minutes for large blobs
     - Configure HttpClient.Timeout and BlobClient request timeout in backend config
     - On timeout exception, return error with specific timeout code: COPY_TIMEOUT
     - Frontend displays error message: "Copy operation timed out. This may occur with very large files or slow network connections."
     - Display "Retry" button to restart operation with same parameters

5. **FR-014: Blob names with special characters**
   - **Question**: How should the system handle blob names with special characters or non-ASCII characters?
   - **Decision**: Allow per Azure rules, validate prohibited chars
   - **Rationale**: Azure supports Unicode characters in blob names; only specific characters are prohibited
   - **Implementation**:
     - Backend validation: Check against Azure prohibited characters: \ / : * ? " < > |
     - Allow all other Unicode characters including spaces, international characters
     - Display validation error: "Blob name contains invalid characters: {list}. The following characters are not allowed: \ / : * ? \" < > |"
     - Reference: https://docs.microsoft.com/azure/storage/blobs/storage-blobs-naming
     - Add unit tests for edge cases (emoji, Chinese characters, accented letters)

6. **Identical source/destination validation**
   - **Question**: Should system prevent copying blob to itself?
   - **Decision**: Validate on form submit and display error: "Source and destination URIs cannot be identical"
   - **Rationale**: Prevents no-op operations, saves unnecessary API calls, improves user experience, prevents potential errors
   - **Implementation**:
     - Frontend: Add case-insensitive URI comparison in CopyForm component before API submission
     - Backend: Add validation check in BlobValidationService (defense-in-depth security)
     - Normalize URIs before comparison (trim whitespace, lowercase, remove trailing slashes)
     - Error code: IDENTICAL_URIS

### Research Output

**Best Practices Applied**:
- Azure SDK for .NET: Use BlobClient.StartCopyFromUriAsync() with polling for progress
- React patterns: Custom hooks (useBlobCopy, useProgress) for state management
- Testing: Test-first approach per constitution; mock Azure SDK in unit tests; integration tests against real Storage Emulator locally, Azure Storage in CI
- Error handling: Try-catch in service layer with specific exception types mapped to user-friendly messages
- Performance: Stream large blobs; use BlobTransferOptions for concurrent transfer optimization

**Generated Artifacts**:
- research.md (this section, fully resolved)
- No external research tasks needed; decisions grounded in Azure SDK documentation and React best practices

---

## Phase 1: Design & API Contracts

### Data Model

**Entity: BlobCopyOperation**
```
{
  "id": UUID (generated by backend)
  "sourceUri": String (validated Azure Blob URI)
  "destinationUri": String (validated Azure Blob URI)
  "status": Enum (pending | in-progress | completed | failed | cancelled)
  "progressPercentage": Number (0-100)
  "bytesTransferred": Number
  "totalBytes": Number
  "estimatedTimeRemaining": Number (seconds, calculated)
  "errorMessage": String (null if no error)
  "startedAt": DateTime (ISO8601)
  "completedAt": DateTime (ISO8601, null if incomplete)
  "correlationId": String (for Application Insights tracing)
}
```

**State Machine**:
```
pending → in-progress → completed
                     ├→ failed
                     └→ cancelled
```

### API Contracts

**Endpoint 1: POST /api/v1/blob-copy/validate**
- Request: { sourceUri: string, destinationUri: string }
- Response 200: { valid: boolean, errors: string[] }
- Response 400: { error: "Invalid request", details: string }
- Response 401: { error: "Unauthorized" }

**Endpoint 2: POST /api/v1/blob-copy/start**
- Request: { sourceUri: string, destinationUri: string }
- Response 202: { id: UUID, status: "pending", ... (BlobCopyOperation) }
- Response 400: { error: "Invalid URIs" }
- Response 401: { error: "Unauthorized" }

**Endpoint 3: GET /api/v1/blob-copy/{id}/status**
- Response 200: { id: UUID, status: string, progressPercentage: number, ... }
- Response 404: { error: "Copy operation not found" }

**SignalR Hub: BlobCopyHub**
- Client method: `SendProgressUpdate(id: UUID, progress: ProgressUpdate)`
- Event data: { bytesTransferred, totalBytes, percentage, estimatedSeconds }

**Validation Rules**:
- sourceUri: must match pattern `https://[account].blob.core.windows.net/[container]/[blob]`
- destinationUri: must match same pattern
- sourceUri !== destinationUri
- Both URIs must be accessible (preliminary check on server-side)

### Generated Design Artifacts

**Files to create in Phase 1**:
- [data-model.md](data-model.md): Detailed entity definitions, state machines, validation rules
- [quickstart.md](quickstart.md): Local development setup, run instructions
- [contracts/api.openapi.yaml](contracts/api.openapi.yaml): Full OpenAPI 3.0 spec
- [contracts/signalr.events.md](contracts/signalr.events.md): SignalR event documentation

### Agent Context Update

After Phase 1 design completion, run:
```bash
.\.specify\scripts\powershell\update-agent-context.ps1 -AgentType copilot
```

This updates the AI agent context with:
- Technology stack: C# .NET 10, React 18, Azure Blob Storage, SignalR
- Architecture: REST API + WebSocket real-time updates
- Unit testing frameworks: xUnit (backend), Vitest (frontend)
- Integration/E2E testing framework: Playwright with cross-browser support (Chrome, Firefox, Safari)
- Key services: BlobCopyService, ProgressNotificationService, BlobValidationService
- Test patterns: Playwright for browser automation, xUnit for service/backend integration

---

## Next Steps

1. **Phase 0 Completion**: Run `/speckit.plan` again to generate `research.md` with all clarifications documented
2. **Phase 1 Completion**: Generate data model, API contracts, quickstart guide
3. **Phase 2**: Run `/speckit.tasks` to break plan into actionable development tasks
4. **Development**: Follow task checklist; maintain ≥80% test coverage; ensure all PRs pass Copilot review

**Status**: Ready for Phase 0/1 execution (all decisions made, no blocking unknowns)

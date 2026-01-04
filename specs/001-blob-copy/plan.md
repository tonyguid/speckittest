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
│   │   │   │   └── useProgress.ts               # Custom hook for progress tracking
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

---

## Phase 0: Research & Clarification

### Outstanding NEEDS CLARIFICATION Items (from spec)

1. **FR-009**: Destination blob already exists behavior
   - **Clarification**: When user attempts to copy to a URI where a blob already exists, should the system:
     - **Decision**: Prompt user with three options: (a) Overwrite, (b) Cancel, (c) Choose new destination
     - **Rationale**: Prevents accidental data loss; gives user control
     - **Implementation**: Add checkbox/dialog in React form before initiating copy

2. **FR-010**: Authentication method
   - **Decision**: Azure Entra ID (managed identity) for backend service principal, with user sign-in via Entra ID in frontend SPA
   - **Rationale**: Aligns with constitution principle 10; no credentials in code; leverages Azure native auth
   - **Implementation**: Use Azure.Identity.DefaultAzureCredential for backend, MSAL.js for frontend

3. **Timeout handling** (Edge Case):
   - **Decision**: Set blob copy timeout to 30 minutes for large blobs; if exceeded, return timeout error with retry button
   - **Rationale**: Reasonable default for multi-gigabyte copies; matches Azure SDK defaults
   - **Implementation**: Configure HttpClientTimeout and BlobClient timeout in backend config

4. **Identical source/destination** (Edge Case):
   - **Decision**: Validate on form submit and display error: "Source and destination URIs must be different"
   - **Rationale**: Prevents no-op operations and user confusion
   - **Implementation**: Add validation check in BlobValidationService

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

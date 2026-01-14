# Tasks: Blob Storage Copy Feature

**Input**: Design documents from `/specs/001-blob-copy/`
**Prerequisites**: plan.md (required), spec.md (required for user stories), data-model.md, contracts/
**Branch**: `001-blob-copy`
**Stack**: C# .NET 10, React 18+, xUnit, Vitest, Playwright, SignalR, Azure Blob Storage

**Tests**: Tests are included as this is a production feature requiring >=80% coverage per constitution.

**Organization**: Tasks are grouped by user story to enable independent implementation and testing.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- Include exact file paths in descriptions

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and basic structure

- [X] T001 Create project structure per plan.md (backend: src/backend/BlobCopy.API/, frontend: src/frontend/)
- [X] T002 [P] Initialize .NET 10 backend project with required NuGet packages (Azure.Storage.Blobs, Azure.Identity, Microsoft.AspNetCore.SignalR) in src/backend/BlobCopy.API/BlobCopy.API.csproj
- [X] T003 [P] Initialize React 18+ frontend project with TypeScript, Axios, @microsoft/signalr in src/frontend/package.json
- [X] T004 [P] Configure ESLint, Prettier for frontend in src/frontend/
- [X] T005 [P] Configure EditorConfig and code style for backend in src/backend/

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core infrastructure that MUST be complete before ANY user story can be implemented

**CRITICAL**: No user story work can begin until this phase is complete

### Data Models

- [X] T006 [P] Create BlobCopyOperation model in src/backend/BlobCopy.API/Models/BlobCopyOperation.cs
- [X] T007 [P] Create BlobCopyStatus enum in src/backend/BlobCopy.API/Models/BlobCopyStatus.cs
- [X] T008 [P] Create ProgressUpdate model in src/backend/BlobCopy.API/Models/ProgressUpdate.cs
- [X] T009 [P] Create ValidationError model in src/backend/BlobCopy.API/Models/ValidationError.cs
- [X] T010 [P] Create CopyRequest DTO in src/backend/BlobCopy.API/Models/CopyRequest.cs
- [X] T011 [P] Create ValidationResult model in src/backend/BlobCopy.API/Models/ValidationResult.cs
- [X] T011b [P] Create CopyResult model in src/backend/BlobCopy.API/Models/CopyResult.cs
- [X] T012 [P] Create TypeScript interfaces (BlobCopyOperation, ProgressUpdate, ValidationError, CopyResult) in src/frontend/src/types.ts

### SignalR Infrastructure

- [X] T013 Create IBlobCopyClient interface in src/backend/BlobCopy.API/Hubs/IBlobCopyClient.cs
- [X] T014 Implement BlobCopyHub SignalR hub in src/backend/BlobCopy.API/Hubs/BlobCopyHub.cs (depends on T013)

### Dependency Injection & Configuration

- [X] T015 Configure DI container, CORS, SignalR mapping in src/backend/BlobCopy.API/Program.cs (depends on T006-T014)
- [X] T016 [P] Create appsettings.json with Azure Storage config in src/backend/BlobCopy.API/appsettings.json
- [X] T017 [P] Create appsettings.Development.json for local dev in src/backend/BlobCopy.API/appsettings.Development.json

### Error Handling Middleware

- [X] T018 Implement ErrorHandlingMiddleware in src/backend/BlobCopy.API/Middleware/ErrorHandlingMiddleware.cs

### Health Check

- [X] T019 Implement HealthController with GET /api/v1/health in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (integrated)

**Checkpoint**: Foundation ready - user story implementation can now begin

---

## Phase 3: User Story 1 - Copy Blob with Progress Tracking (Priority: P1)

**Goal**: User copies a blob from source to destination URI and sees real-time progress updates until completion.

**Independent Test**: Provide valid source and destination blob URIs, initiate copy, observe progress updates, verify blob appears at destination.

### Backend Services for US1

- [X] T020 [US1] Implement CopyOperationManager (in-memory operation store) in src/backend/BlobCopy.API/Services/BlobCopyService.cs (integrated)
- [X] T021 [US1] Implement IBlobClientFactory interface in src/backend/BlobCopy.API/Services/IBlobClientFactory.cs
- [X] T022 [US1] Implement BlobClientFactory (Azure SDK wrapper) in src/backend/BlobCopy.API/Services/BlobClientFactory.cs (depends on T021)
- [X] T023 [US1] Implement ProgressNotificationService in src/backend/BlobCopy.API/Services/ProgressNotificationService.cs (depends on T014)
- [X] T024 [US1] Implement BlobCopyService (core copy logic with progress tracking) in src/backend/BlobCopy.API/Services/BlobCopyService.cs (depends on T020, T022, T023)

### Backend API Endpoints for US1

- [X] T025 [US1] Implement POST /api/BlobCopy/start endpoint in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (depends on T024)
- [X] T026 [US1] Implement GET /api/BlobCopy/status/{id} endpoint in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (depends on T020)

### Frontend Services for US1

- [X] T027 [P] [US1] Implement signalRService (WebSocket connection) in src/frontend/src/services/signalRClient.ts
- [X] T028 [P] [US1] Implement blobCopyApiService (HTTP client) in src/frontend/src/services/apiClient.ts
- [X] T029 [US1] Implement useSignalR custom hook in src/frontend/src/hooks/useProgress.ts (depends on T027)
- [X] T030 [US1] Implement useBlobCopy custom hook in src/frontend/src/hooks/useCopyOperation.ts (depends on T028, T029)

### Frontend Components for US1

- [X] T031 [P] [US1] Implement ProgressDisplay component in src/frontend/src/components/ProgressDisplay.tsx
- [X] T032 [P] [US1] Implement CopyButton component in src/frontend/src/components/CopyButton.tsx
- [X] T033 [US1] Implement BlobCopyForm component (basic, without validation errors) in src/frontend/src/components/CopyForm.tsx (depends on T032)
- [X] T034 [US1] Implement ResultMessage component (success state) in src/frontend/src/components/ResultDisplay.tsx
- [X] T035 [US1] Wire up App.tsx with copy flow in src/frontend/src/App.tsx (depends on T030, T031, T033, T034)

### Backend Tests for US1

- [X] T036 [P] [US1] Unit tests for BlobCopyService in src/backend/BlobCopy.Tests/Unit/Services/BlobCopyServiceTests.cs (depends on T024)
- [X] T037 [P] [US1] Unit tests for ProgressNotificationService in src/backend/BlobCopy.Tests/Unit/Services/ProgressNotificationServiceTests.cs (depends on T023)
- [X] T038 [P] [US1] Unit tests for CopyOperationManager in src/backend/BlobCopy.Tests/Unit/Services/BlobCopyServiceTests.cs (integrated)

### Frontend Tests for US1

- [X] T039 [P] [US1] Unit tests for ProgressDisplay in src/frontend/src/__tests__/components/ProgressDisplay.test.tsx (depends on T031)
- [X] T040 [P] [US1] Unit tests for useBlobCopy hook in src/frontend/src/__tests__/hooks/useCopyOperation.test.ts (depends on T030)
- [X] T041 [P] [US1] Unit tests for useSignalR hook in src/frontend/src/__tests__/hooks/useProgress.test.ts (depends on T029)
- [X] T042 [P] [US1] Unit tests for blobCopyApiService in src/frontend/src/__tests__/services/apiClient.test.ts (depends on T028)

### E2E Tests for US1

- [X] T043 [US1] Playwright setup and config in src/frontend/playwright.config.ts
- [X] T044 [US1] E2E test for copy blob with progress flow in src/frontend/e2e/blob-copy.spec.ts (depends on T043)

**Checkpoint**: User Story 1 should be fully functional - user can copy a blob and see real-time progress

---

## Phase 4: User Story 2 - Handle Copy Failures Gracefully (Priority: P2)

**Goal**: When copy fails (invalid URIs, auth issues, network errors), display clear actionable error messages to user.

**Independent Test**: Provide invalid URIs, expired credentials, or simulate network failures; verify appropriate error messages display.

### Backend Services for US2

- [X] T045 [US2] Enhance BlobCopyService with error handling and error code mapping in src/backend/BlobCopy.API/Services/BlobCopyService.cs

### Backend API Endpoints for US2

- [X] T046 [US2] Implement POST /api/BlobCopy/cancel/{id} endpoint in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (depends on T020)
- [X] T047 [US2] Enhance error responses with proper HTTP status codes and error details in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs

### Frontend Components for US2

- [X] T048 [US2] Enhance ResultMessage component with error state and retry button in src/frontend/src/components/ResultDisplay.tsx
- [X] T049 [US2] Add cancel operation support to useBlobCopy hook in src/frontend/src/hooks/useCopyOperation.ts
- [X] T050 [US2] Add error handling to BlobCopyForm (display API errors) in src/frontend/src/components/CopyForm.tsx

### Backend Tests for US2

- [X] T051 [P] [US2] Unit tests for error handling in BlobCopyService in src/backend/BlobCopy.Tests/Unit/Services/BlobCopyServiceTests.cs (depends on T045)
- [X] T052 [P] [US2] Integration tests for error responses in BlobCopyController in src/backend/BlobCopy.Tests/Integration/Controllers/ (depends on T047)

### Frontend Tests for US2

- [X] T053 [P] [US2] Unit tests for ResultMessage error state in src/frontend/src/__tests__/components/ResultDisplay.test.tsx (depends on T048)
- [X] T054 [P] [US2] Unit tests for cancel operation in useBlobCopy in src/frontend/src/__tests__/hooks/useCopyOperation.test.ts (depends on T049)

### Destination Conflict Resolution for US2 (FR-009)

- [X] T047b [US2] Add destination-exists check to /start endpoint returning 409 Conflict with conflict details in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (depends on T024)
- [X] T048b [P] [US2] Implement OverwriteConfirmDialog component (overwrite/cancel/rename options) in src/frontend/src/components/OverwriteConfirmDialog.tsx
- [X] T049b [US2] Add conflict resolution flow to useBlobCopy hook (handle 409, show dialog, resubmit with user choice) in src/frontend/src/hooks/useCopyOperation.ts
- [X] T053b [P] [US2] Unit tests for OverwriteConfirmDialog in src/frontend/src/__tests__/components/OverwriteConfirmDialog.test.tsx (depends on T048b)

### E2E Tests for US2

- [X] T055 [US2] E2E test for error handling scenarios in src/frontend/e2e/blob-copy.spec.ts (integrated, depends on T043)
- [X] T055b [US2] E2E test for destination conflict flow in src/frontend/e2e/blob-copy-conflict.spec.ts (depends on T043)
- [X] T056 [US2] E2E test for cancel operation in src/frontend/e2e/blob-copy.spec.ts (integrated, depends on T043)

**Checkpoint**: User Story 2 complete - errors are handled gracefully with actionable messages

---

## Phase 5: User Story 3 - Input Validation (Priority: P2)

**Goal**: Validate blob URIs before copy, ensure proper format, prevent identical source/destination.

**Independent Test**: Provide malformed URIs, empty fields, identical URIs; verify validation errors display correctly.

### Backend Services for US3

- [X] T057 [US3] Implement BlobValidationService in src/backend/BlobCopy.API/Services/BlobValidationService.cs

### Backend API Endpoints for US3

- [X] T058 [US3] Implement POST /api/BlobCopy/validate endpoint in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (depends on T057)
- [X] T059 [US3] Add validation to /start endpoint (call BlobValidationService before copy) in src/backend/BlobCopy.API/Controllers/BlobCopyController.cs (depends on T057)

### Frontend Services for US3

- [X] T060 [P] [US3] Implement uriValidationService (client-side format validation) in src/frontend/src/services/uriValidationService.ts

### Frontend Components for US3

- [X] T061 [US3] Enhance BlobCopyForm with client-side validation (format, identical URIs) in src/frontend/src/components/CopyForm.tsx (depends on T060)
- [X] T062 [US3] Add server-side validation call before copy in useBlobCopy hook in src/frontend/src/hooks/useCopyOperation.ts

### Backend Tests for US3

- [X] T063 [P] [US3] Unit tests for BlobValidationService in src/backend/BlobCopy.Tests/Unit/Services/BlobValidationServiceTests.cs (depends on T057)
- [X] T064 [P] [US3] Integration tests for /validate endpoint in src/backend/BlobCopy.Tests/Integration/Controllers/ValidateEndpointIntegrationTests.cs (depends on T058)

### Frontend Tests for US3

- [X] T065 [P] [US3] Unit tests for uriValidationService in src/frontend/src/__tests__/services/uriValidationService.test.ts (depends on T060)
- [X] T066 [P] [US3] Unit tests for BlobCopyForm validation in src/frontend/src/__tests__/components/CopyForm.test.tsx (depends on T061)

### E2E Tests for US3

- [X] T067 [US3] E2E test for input validation in src/frontend/e2e/blob-copy.spec.ts (integrated, depends on T043)

**Checkpoint**: User Story 3 complete - all input validation working on client and server

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories

### Integration Tests

- [X] T068 [P] Full integration tests for BlobCopyController (all endpoints) in src/backend/BlobCopy.Tests/Integration/Controllers/
- [X] T069 [P] API contract validation tests in src/backend/BlobCopy.Tests/Unit/ApiContractTests.cs

### Accessibility & Quality

- [X] T070 E2E test for WCAG AA accessibility in src/frontend/e2e/blob-copy.spec.ts (integrated)
- [X] T071 E2E test for health check endpoint in src/frontend/e2e/blob-copy.spec.ts (integrated)

### Documentation & CI/CD

- [X] T072 [P] Create/update OpenAPI spec in specs/001-blob-copy/contracts/api.openapi.yaml
- [X] T073 [P] Create GitHub Actions CI/CD workflow in .github/workflows/ci.yml
- [X] T074 Update quickstart.md with final setup instructions in specs/001-blob-copy/quickstart.md

### Observability

- [X] T075 [P] Integrate Application Insights in backend in src/backend/BlobCopy.API/Services/TelemetryService.cs
- [X] T076 [P] Integrate Application Insights in frontend in src/frontend/src/services/applicationInsightsService.ts

### Performance Tests (SC-001, SC-004, Constitution Principle 5)

- [X] T079 [P] Add performance benchmark for /validate endpoint (<200ms p95) in src/backend/BlobCopy.Tests/Performance/ValidationPerformanceTests.cs
- [X] T080 [P] Add performance benchmark for copy operation (<1 min for 100MB) in src/backend/BlobCopy.Tests/Performance/CopyPerformanceTests.cs
- [X] T081 [P] Configure performance test execution in CI (nightly job) in .github/workflows/performance-tests.yml

### Infrastructure (Optional for MVP)

- [X] T082 [P] Create Terraform infrastructure files in infra/terraform/main.tf
- [X] T083 [P] Create deployment documentation in docs/DEPLOYMENT.md

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3-5)**: All depend on Foundational phase completion
  - User stories can proceed in parallel (if staffed)
  - Or sequentially in priority order (US1 -> US2 -> US3)
- **Polish (Phase 6)**: Depends on all user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Builds on US1 components but independently testable
- **User Story 3 (P2)**: Can start after Foundational (Phase 2) - Builds on US1 components but independently testable

### Within Each User Story

- Backend services before API endpoints
- API endpoints before frontend services
- Frontend services before components
- Components before E2E tests
- Unit tests can run in parallel with implementation (TDD)

### Parallel Opportunities

**Phase 1 - All can run in parallel:**
- T002, T003, T004, T005

**Phase 2 - Models can run in parallel:**
- T006, T007, T008, T009, T010, T011, T012

**Phase 3 (US1) - Parallel groups:**
- Frontend services: T027, T028
- Frontend components: T031, T032
- Backend tests: T036, T037, T038
- Frontend tests: T039, T040, T041, T042

**Phase 4 (US2) - Parallel groups:**
- Backend tests: T051, T052
- Frontend tests: T053, T054
- Conflict resolution: T048b (component), T053b (tests)

**Phase 5 (US3) - Parallel groups:**
- T060 (frontend service)
- Backend tests: T063, T064
- Frontend tests: T065, T066

**Phase 6 - Parallel groups:**
- Integration tests: T068, T069
- Documentation: T072, T073, T074
- Observability: T075, T076
- Performance tests: T079, T080, T081
- Infrastructure: T082, T083

---

## Parallel Example: User Story 1 Backend

```bash
# Launch all backend unit tests together (after services implemented):
Task: "Unit tests for BlobCopyService in src/backend/BlobCopy.Tests/Unit/BlobCopyServiceTests.cs"
Task: "Unit tests for ProgressNotificationService in src/backend/BlobCopy.Tests/Unit/ProgressNotificationServiceTests.cs"
Task: "Unit tests for CopyOperationManager in src/backend/BlobCopy.Tests/Unit/CopyOperationManagerTests.cs"

# Launch frontend services together:
Task: "Implement signalRService in src/frontend/BlobCopy.UI/src/services/signalRService.ts"
Task: "Implement blobCopyApiService in src/frontend/BlobCopy.UI/src/services/blobCopyApiService.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL - blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Test User Story 1 independently
5. Deploy/demo if ready - core copy functionality works

### Incremental Delivery

1. Complete Setup + Foundational -> Foundation ready
2. Add User Story 1 -> Test independently -> Deploy/Demo (MVP!)
3. Add User Story 2 -> Test independently -> Deploy/Demo (error handling)
4. Add User Story 3 -> Test independently -> Deploy/Demo (validation)
5. Add Polish -> Final release

### Suggested MVP Scope

For initial deployment, complete:
- Phase 1: Setup (T001-T005)
- Phase 2: Foundational (T006-T019)
- Phase 3: User Story 1 (T020-T044)

This delivers the core copy-with-progress functionality.

---

## Summary

| Phase | Tasks | Description |
|-------|-------|-------------|
| Phase 1: Setup | T001-T005 (5 tasks) | Project initialization |
| Phase 2: Foundational | T006-T019 + T011b (15 tasks) | Core infrastructure, models, SignalR |
| Phase 3: User Story 1 | T020-T044 (25 tasks) | Copy blob with progress tracking |
| Phase 4: User Story 2 | T045-T056 + T047b-T055b (17 tasks) | Error handling, cancellation, conflict resolution |
| Phase 5: User Story 3 | T057-T067 (11 tasks) | Input validation |
| Phase 6: Polish | T068-T083 (16 tasks) | Integration tests, CI/CD, docs, performance |
| **TOTAL** | **89 tasks** | |

### Tasks per User Story

- **User Story 1 (P1)**: 25 tasks (copy with progress)
- **User Story 2 (P2)**: 17 tasks (error handling, cancellation, FR-009 conflict resolution)
- **User Story 3 (P2)**: 11 tasks (validation)

### Independent Test Criteria

- **US1**: Valid URIs -> copy works -> progress shows -> success message displays -> blob at destination
- **US2**: Invalid URIs or errors -> appropriate error message -> retry available
- **US3**: Malformed URIs -> validation error on client/server -> copy prevented

---

## Notes

- [P] tasks = different files, no dependencies - can run in parallel
- [Story] label maps task to specific user story for traceability
- Each user story is independently completable and testable
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Target >=80% code coverage per constitution

# Tasks: User Authentication (Login)

**Input**: Design documents from `/specs/002-login/`
**Prerequisites**: [plan.md](plan.md) (required), [spec.md](spec.md) (required for user stories), [research.md](research.md), [data-model.md](data-model.md), contracts/

**Tests**: This feature does not explicitly request TDD approach in the spec, so test tasks are provided but optional. They can be implemented after the core functionality is working.

**Organization**: Tasks are grouped by user story (US1: User Login, US2: Error Handling, US3: Session Management) to enable independent implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (e.g., US1, US2, US3)
- **US0**: Setup and foundational tasks
- Include exact file paths in descriptions

## Path Conventions

This project uses web app structure:
- Backend: `src/backend/BlobCopy.API/`
- Frontend: `src/frontend/`
- Backend Tests: `src/backend/BlobCopy.Tests/`
- Frontend Tests: `src/frontend/tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project initialization and environment setup for authentication feature

- [ ] T001 [US0] Install backend NuGet packages: Microsoft.AspNetCore.Authentication.JwtBearer (9.0.0), Microsoft.Identity.Web (2.15.0), Microsoft.Graph (5.35.0)
- [ ] T002 [US0] Install frontend npm packages: @azure/msal-react (^3.0.0), @azure/msal-browser (^3.0.0)
- [ ] T003 [P] [US0] Create Entra ID application registration following `specs/002-login/quickstart.md` steps 1-6
- [ ] T004 [P] [US0] Configure backend `appsettings.Development.json` with Entra ID credentials (TenantId, ClientId, ClientSecret)
- [ ] T005 [P] [US0] Configure frontend `.env.local` with Entra ID configuration (VITE_ENTRA_TENANT_ID, VITE_ENTRA_CLIENT_ID, VITE_REDIRECT_URI)

**Checkpoint**: Environment configured - authentication infrastructure can now be built

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Core authentication infrastructure that MUST be complete before ANY user story can be implemented

**⚠️ CRITICAL**: No user story work can begin until this phase is complete

- [ ] T006 [P] [US0] Create EntraIdOptions configuration class in `src/backend/BlobCopy.API/Configuration/EntraIdOptions.cs`
- [ ] T007 [P] [US0] Create UserSession model in `src/backend/BlobCopy.API/Models/UserSession.cs` (per data-model.md)
- [ ] T008 [P] [US0] Create AuthenticationToken model in `src/backend/BlobCopy.API/Models/AuthenticationToken.cs` (per data-model.md)
- [ ] T009 [P] [US0] Create AuthenticationResult model in `src/backend/BlobCopy.API/Models/AuthenticationResult.cs` (per data-model.md)
- [ ] T010 [US0] Configure JWT authentication in `src/backend/BlobCopy.API/Program.cs` (add authentication services, JWT Bearer configuration)
- [ ] T011 [US0] Create MSAL configuration in `src/frontend/src/auth/authConfig.ts` with PublicClientApplication setup
- [ ] T012 [P] [US0] Create React auth context in `src/frontend/src/auth/authContext.tsx` with MsalProvider wrapper
- [ ] T013 [P] [US0] Create useAuth custom hook in `src/frontend/src/auth/useAuth.ts` for auth state and operations

**Checkpoint**: Foundation ready - user story implementation can now begin in parallel

---

## Phase 3: User Story 1 - User Login with Entra ID (Priority: P1) 🎯 MVP

**ADO ID**: 3067

**Goal**: Enable users to authenticate using "Sign in with Microsoft" button, be redirected to Entra ID for authentication, and return to the blob copy page with an authenticated session displaying their name, email, and avatar.

**Independent Test**: Navigate to app → Redirected to login page → Click "Sign in with Microsoft" → Authenticate with Entra ID → Redirected to blob copy page → See name, email, avatar in header.

### Backend Implementation for User Story 1

- [ ] T014 [P] [US1] Create IAuthenticationService interface in `src/backend/BlobCopy.API/Services/IAuthenticationService.cs`
- [ ] T015 [US1] Implement AuthenticationService in `src/backend/BlobCopy.API/Services/AuthenticationService.cs` (token exchange, validation, user info extraction)
- [ ] T016 [P] [US1] Create IUserService interface in `src/backend/BlobCopy.API/Services/IUserService.cs`
- [ ] T017 [US1] Implement UserService in `src/backend/BlobCopy.API/Services/UserService.cs` (extract user claims from JWT, fetch avatar from Microsoft Graph)
- [ ] T018 [US1] Create AuthController in `src/backend/BlobCopy.API/Controllers/AuthController.cs` with Login endpoint (GET /auth/login)
- [ ] T019 [US1] Implement OAuth callback handler in AuthController (GET /auth/callback) - exchange authorization code for tokens
- [ ] T020 [P] [US1] Create UserController in `src/backend/BlobCopy.API/Controllers/UserController.cs` with GetCurrentUser endpoint (GET /api/user/me)
- [ ] T021 [US1] Create AuthenticationMiddleware in `src/backend/BlobCopy.API/Middleware/AuthenticationMiddleware.cs` to redirect unauthenticated requests (FR-001, FR-013)
- [ ] T022 [US1] Register authentication services in `src/backend/BlobCopy.API/Program.cs` (dependency injection for IAuthenticationService, IUserService)

### Frontend Implementation for User Story 1

- [ ] T023 [P] [US1] Create LoginButton component in `src/frontend/src/components/LoginButton.tsx`
- [ ] T024 [P] [US1] Create UserProfile component in `src/frontend/src/components/UserProfile.tsx` to display name, email, avatar
- [ ] T025 [P] [US1] Create ProtectedRoute component in `src/frontend/src/components/ProtectedRoute.tsx` for route guarding
- [ ] T026 [US1] Create LoginPage in `src/frontend/src/pages/LoginPage.tsx` with "Sign in with Microsoft" button
- [ ] T027 [US1] Configure React Router to protect all routes and redirect unauthenticated users to /login
- [ ] T028 [US1] Create API client with token injection in `src/frontend/src/services/apiClient.ts` (Axios interceptor for Authorization header)
- [ ] T029 [US1] Integrate UserProfile component into application header/nav bar to display authenticated user info

### Testing for User Story 1 (Optional)

- [ ] T030 [P] [US1] Write unit tests for AuthenticationService in `src/backend/BlobCopy.Tests/Services/AuthenticationServiceTests.cs`
- [ ] T031 [P] [US1] Write unit tests for AuthController in `src/backend/BlobCopy.Tests/Controllers/AuthControllerTests.cs`
- [ ] T032 [P] [US1] Write React component tests for LoginButton in `src/frontend/tests/unit/auth/LoginButton.test.tsx`
- [ ] T033 [P] [US1] Write React component tests for UserProfile in `src/frontend/tests/unit/auth/UserProfile.test.tsx`
- [ ] T034 [US1] Write E2E test for complete login flow in `src/frontend/tests/e2e/auth.spec.ts` (Playwright)

**Checkpoint**: At this point, User Story 1 should be fully functional - users can log in, see their profile, and access protected pages

---

## Phase 4: User Story 2 - Handle Authentication Failures (Priority: P2)

**ADO ID**: 3068

**Goal**: Display clear, actionable error messages when authentication fails (cancelled login, invalid credentials, network issues, token exchange failures) with guidance on recovery.

**Independent Test**: Cancel Entra ID login → See error message with retry option. Simulate network failure → See appropriate error message.

### Backend Implementation for User Story 2

- [ ] T035 [US2] Add error handling to AuthController callback endpoint - map Entra ID error codes to user-friendly messages (per data-model.md error mapping)
- [ ] T036 [P] [US2] Implement CSRF state validation in AuthController callback (FR-015) - return error if state parameter mismatch
- [ ] T037 [US2] Add error logging to AuthenticationService without exposing sensitive information (NFR-005)

### Frontend Implementation for User Story 2

- [ ] T038 [P] [US2] Create ErrorPage component in `src/frontend/src/pages/ErrorPage.tsx` to display authentication errors with retry button
- [ ] T039 [US2] Add error handling to useAuth hook for InteractionRequiredAuthError and other MSAL errors
- [ ] T040 [US2] Implement error state display in LoginPage for authentication failures (user cancelled, invalid credentials, network error)
- [ ] T041 [US2] Add network error detection and user-friendly messaging in apiClient.ts

### Testing for User Story 2 (Optional)

- [ ] T042 [P] [US2] Write unit tests for error scenarios in AuthenticationService tests
- [ ] T043 [P] [US2] Write React component tests for ErrorPage in `src/frontend/tests/unit/pages/ErrorPage.test.tsx`
- [ ] T044 [US2] Write E2E test for cancelled login flow in `src/frontend/tests/e2e/auth-errors.spec.ts`

**Checkpoint**: At this point, User Stories 1 AND 2 should both work - login succeeds AND failures are handled gracefully

---

## Phase 5: User Story 3 - Session Management (Priority: P2)

**ADO ID**: 3069

**Goal**: Maintain authenticated sessions across page refreshes, automatically refresh tokens before expiration, and provide logout functionality that clears session and redirects to login.

**Independent Test**: Log in → Refresh page → Still logged in. Wait for token to approach expiration → Token refreshes automatically. Click Logout → Redirected to login, session cleared.

### Backend Implementation for User Story 3

- [ ] T045 [US3] Implement token refresh endpoint in AuthController (POST /auth/refresh) - exchange refresh token for new access token
- [ ] T046 [US3] Implement logout endpoint in AuthController (POST /auth/logout) - clear cookies, invalidate tokens, redirect to login
- [ ] T047 [US3] Configure refresh token as HttpOnly cookie in callback handler (NFR-003) - set Secure, SameSite=Strict, Path=/auth/refresh
- [ ] T048 [US3] Add token expiration validation middleware to reject expired tokens on all protected endpoints

### Frontend Implementation for User Story 3

- [ ] T049 [P] [US3] Create LogoutButton component in `src/frontend/src/components/LogoutButton.tsx`
- [ ] T050 [US3] Implement automatic token refresh logic in useAuth hook - trigger refresh at 5 minutes before expiration (SC-004)
- [ ] T051 [US3] Add session persistence on page refresh using MSAL acquireTokenSilent in authContext
- [ ] T052 [US3] Implement logout functionality in useAuth hook - call backend /auth/logout, clear MSAL cache, redirect to login
- [ ] T053 [US3] Add LogoutButton to application header/nav bar next to UserProfile
- [ ] T054 [US3] Add session expired handling - detect failed refresh, show message, redirect to login

### Testing for User Story 3 (Optional)

- [ ] T055 [P] [US3] Write unit tests for token refresh logic in AuthenticationService tests
- [ ] T056 [P] [US3] Write unit tests for logout endpoint in AuthController tests
- [ ] T057 [P] [US3] Write React tests for LogoutButton component
- [ ] T058 [US3] Write E2E test for logout flow in `src/frontend/tests/e2e/auth-logout.spec.ts`
- [ ] T059 [US3] Write E2E test for session persistence across page refresh

**Checkpoint**: All user stories should now be independently functional - login, error handling, and session management all work

---

## Phase 6: Polish & Cross-Cutting Concerns

**Purpose**: Improvements that affect multiple user stories and final validation

- [ ] T060 [P] [US0] Add Application Insights telemetry for authentication metrics (SC-001, SC-002, SC-004 monitoring)
- [ ] T061 [P] [US0] Implement avatar fallback using user initials if Microsoft Graph photo fetch fails (per research.md)
- [ ] T062 [P] [US0] Add security headers to backend responses (Content-Security-Policy, X-Frame-Options, etc.)
- [ ] T063 [P] [US0] Optimize token validation performance - cache Entra ID public keys
- [ ] T064 [US0] Run complete quickstart.md validation end-to-end (Steps 1-7)
- [ ] T065 [P] [US0] Code review and refactoring for security best practices
- [ ] T066 [P] [US0] Update API documentation (Swagger) with authentication examples
- [ ] T067 [US0] Verify all success criteria from spec.md (SC-001 through SC-006)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies - can start immediately
- **Foundational (Phase 2)**: Depends on Setup completion - BLOCKS all user stories
- **User Stories (Phase 3, 4, 5)**: All depend on Foundational phase completion
  - User stories can then proceed in parallel (if staffed)
  - Or sequentially in priority order (US1 → US2 → US3)
- **Polish (Phase 6)**: Depends on all desired user stories being complete

### User Story Dependencies

- **User Story 1 (P1)**: Can start after Foundational (Phase 2) - No dependencies on other stories
- **User Story 2 (P2)**: Can start after Foundational (Phase 2) - Independent of US1 (error handling is separate concern)
- **User Story 3 (P3)**: Can start after Foundational (Phase 2) - Independent but enhances US1 (session management builds on login)

### Within Each User Story

- Backend services before controllers
- Controllers before frontend components
- Core implementation before error handling
- Story complete before moving to next priority

### Parallel Opportunities

- All Setup tasks marked [P] can run in parallel
- All Foundational tasks marked [P] can run in parallel (within Phase 2)
- Once Foundational phase completes, all user stories can start in parallel (if team capacity allows)
- All tests for a user story marked [P] can run in parallel
- Models/services/components within a story marked [P] can run in parallel
- Different user stories can be worked on in parallel by different team members

---

## Parallel Example: User Story 1

### Launch backend models together:
```
Task T007: Create UserSession model
Task T008: Create AuthenticationToken model
Task T009: Create AuthenticationResult model
```

### Launch backend services together (after interfaces created):
```
Task T015: Implement AuthenticationService
Task T017: Implement UserService
```

### Launch frontend components together:
```
Task T023: Create LoginButton component
Task T024: Create UserProfile component
Task T025: Create ProtectedRoute component
```

### Launch all unit tests together (optional):
```
Task T030: Unit tests for AuthenticationService
Task T031: Unit tests for AuthController
Task T032: Component tests for LoginButton
Task T033: Component tests for UserProfile
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup (T001-T005)
2. Complete Phase 2: Foundational (T006-T013) - CRITICAL - blocks all stories
3. Complete Phase 3: User Story 1 (T014-T034)
4. **STOP and VALIDATE**: Test User Story 1 independently per acceptance scenarios in spec.md
5. Deploy/demo if ready

**MVP Deliverable**: Users can log in with Microsoft, see their profile, and access the blob copy page. This satisfies the core authentication requirement.

### Incremental Delivery

1. Complete Setup + Foundational → Foundation ready (T001-T013)
2. Add User Story 1 → Test independently → Deploy/Demo (MVP!) (T014-T034)
3. Add User Story 2 → Test independently → Deploy/Demo (T035-T044)
4. Add User Story 3 → Test independently → Deploy/Demo (T045-T059)
5. Polish & validate → Final release (T060-T067)

Each story adds value without breaking previous stories.

### Parallel Team Strategy

With multiple developers:

1. Team completes Setup + Foundational together (T001-T013)
2. Once Foundational is done:
   - **Developer A**: User Story 1 - Login flow (T014-T034)
   - **Developer B**: User Story 2 - Error handling (T035-T044)
   - **Developer C**: User Story 3 - Session management (T045-T059)
3. Stories complete and integrate independently
4. Team collaborates on Polish phase (T060-T067)

---

## Testing Strategy

### Unit Tests (Optional - can be done after core functionality)

- Backend: xUnit tests for services and controllers
- Frontend: Vitest for React components and hooks
- Focus on critical paths: token exchange, token validation, error mapping

### Integration Tests (Optional)

- Full OAuth flow from frontend → backend → Entra ID → callback
- Token refresh flow
- Logout flow

### E2E Tests (Recommended)

- Playwright tests for complete user journeys
- User Story 1: Complete login flow
- User Story 2: Authentication failure scenarios
- User Story 3: Logout and session persistence

### Manual Testing Checklist (Required)

Per `quickstart.md` Step 6:
- [ ] Navigate to app, redirected to login
- [ ] Click "Sign in with Microsoft", authenticate, see profile
- [ ] Refresh page, still logged in
- [ ] Wait for token refresh (or modify expiration for testing)
- [ ] Click Logout, redirected to login
- [ ] Cancel login, see error message
- [ ] Verify HTTPS required for cookies in production

---

## Success Criteria Validation

Before marking feature complete, verify against spec.md:

- [ ] **SC-001**: Authentication completes in <10s (measure with Application Insights)
- [ ] **SC-002**: 99% success rate for completed auth flows (monitor with telemetry)
- [ ] **SC-003**: Error messages display within 2s (E2E test validation)
- [ ] **SC-004**: Token refresh occurs ≥5min before expiration (unit test + monitoring)
- [ ] **SC-005**: Logout completes in <2s (E2E test validation)
- [ ] **SC-006**: 85% positive user feedback on login UX (post-release survey)

---

## Notes

- **[P]** tasks = different files, no dependencies within same phase
- **[Story]** label maps task to specific user story for traceability to ADO work items
- Each user story should be independently completable and testable
- Tests are optional - can be implemented after core functionality or skipped if spec doesn't require TDD
- Commit after each task or logical group
- Stop at any checkpoint to validate story independently
- Avoid: vague tasks, same file conflicts, cross-story dependencies that break independence
- Reference `data-model.md` for entity structure
- Reference `contracts/` for API endpoint specifications
- Reference `research.md` for technical decisions and rationale

---

## Task Summary

- **Total Tasks**: 67
- **Setup Phase**: 5 tasks
- **Foundational Phase**: 8 tasks (blocking)
- **User Story 1 (P1)**: 21 tasks (16 implementation + 5 tests)
- **User Story 2 (P2)**: 10 tasks (7 implementation + 3 tests)
- **User Story 3 (P3)**: 15 tasks (11 implementation + 4 tests)
- **Polish Phase**: 8 tasks
- **Parallelizable Tasks**: 32 tasks marked [P]
- **Test Tasks**: 12 tasks (optional, can be omitted or done after implementation)

**MVP Scope** (Recommended): Phase 1 + Phase 2 + Phase 3 (User Story 1) = 34 tasks

**Full Feature**: All 67 tasks (or 55 tasks if tests are skipped)

---

**Generated by**: Claude Code `/speckit.tasks` command | **Date**: January 20, 2026

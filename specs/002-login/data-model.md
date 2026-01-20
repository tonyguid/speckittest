# Data Model: User Authentication (Login)

**Feature**: 002-login | **Date**: January 20, 2026 | **Plan**: [plan.md](plan.md)

This document defines the core entities, their relationships, validation rules, and state transitions for the User Authentication feature.

---

## Entity Overview

```
┌─────────────────────┐
│ AuthenticationToken │
│                     │
│ - accessToken       │◄──┐
│ - refreshToken      │   │
│ - expiresIn         │   │
│ - tokenType         │   │
└─────────────────────┘   │
                          │
                          │ contains
                          │
┌─────────────────────┐   │
│ AuthenticationResult│───┘
│                     │
│ - success           │
│ - userSession       │◄──┐
│ - errorCode         │   │
│ - errorMessage      │   │
└─────────────────────┘   │
                          │
                          │ creates
                          │
┌─────────────────────┐   │
│   UserSession       │───┘
│                     │
│ - userId            │
│ - name              │
│ - email             │
│ - avatar            │
│ - accessTokenExp    │
│ - isAuthenticated   │
└─────────────────────┘
```

---

## Entity Definitions

### 1. UserSession

Represents an authenticated user's session state.

**Source Requirements**: FR-006, FR-007, FR-009

#### Fields

| Field | Type | Required | Description | Constraints |
|-------|------|----------|-------------|-------------|
| `userId` | string | Yes | Unique identifier from Entra ID (OID claim) | GUID format |
| `name` | string | Yes | User's full display name | 1-256 characters |
| `email` | string | Yes | User's email address | Valid email format |
| `avatar` | string? | No | URL to user's profile photo | Valid HTTPS URL or null |
| `accessTokenExpiration` | DateTime | Yes | When the access token expires | Future timestamp |
| `isAuthenticated` | boolean | Yes | Whether user is currently authenticated | true/false |

#### Validation Rules

```typescript
interface UserSession {
  userId: string;          // Must be valid GUID
  name: string;           // Length: 1-256, not empty
  email: string;          // Must match email regex
  avatar: string | null;  // If present, must be valid HTTPS URL
  accessTokenExpiration: Date; // Must be > current time
  isAuthenticated: boolean;
}

// Validation
function validateUserSession(session: UserSession): ValidationResult {
  if (!isValidGuid(session.userId))
    return invalid("userId must be a valid GUID");

  if (session.name.trim().length === 0 || session.name.length > 256)
    return invalid("name must be 1-256 characters");

  if (!isValidEmail(session.email))
    return invalid("email must be valid email address");

  if (session.avatar && !session.avatar.startsWith("https://"))
    return invalid("avatar must be HTTPS URL or null");

  if (session.accessTokenExpiration <= new Date())
    return invalid("accessTokenExpiration must be in the future");

  return valid();
}
```

#### State Transitions

```
┌──────────┐
│   NEW    │ (User not authenticated)
└────┬─────┘
     │ login()
     ▼
┌──────────┐
│  ACTIVE  │ (User authenticated, token valid)
└────┬─────┘
     │ token expires
     ▼
┌──────────┐
│ EXPIRED  │ (Token expired, needs refresh)
└────┬─────┘
     │ refresh() success
     ▼
┌──────────┐
│  ACTIVE  │
└────┬─────┘
     │ refresh() failure OR logout()
     ▼
┌──────────┐
│ INVALID  │ (Session terminated)
└──────────┘
```

#### Business Rules

1. **Active Session**: `isAuthenticated === true && accessTokenExpiration > now()`
2. **Expired Session**: `accessTokenExpiration <= now()`
3. **Invalid Session**: `isAuthenticated === false`
4. **Auto-Refresh Trigger**: When `now() > accessTokenExpiration - 5 minutes` (SC-004)

#### Backend Representation (C#)

```csharp
public class UserSession
{
    [Required]
    public Guid UserId { get; set; }

    [Required]
    [StringLength(256, MinimumLength = 1)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Url]
    public string? Avatar { get; set; }

    [Required]
    public DateTime AccessTokenExpiration { get; set; }

    [Required]
    public bool IsAuthenticated { get; set; }

    // Helper methods
    public bool IsExpired() => DateTime.UtcNow >= AccessTokenExpiration;
    public bool NeedsRefresh() => DateTime.UtcNow >= AccessTokenExpiration.AddMinutes(-5);
}
```

---

### 2. AuthenticationToken

Represents the OAuth 2.0 tokens returned by Entra ID.

**Source Requirements**: FR-004, FR-009, FR-014, NFR-003

#### Fields

| Field | Type | Required | Description | Constraints |
|-------|------|----------|-------------|-------------|
| `accessToken` | string | Yes | JWT access token | Valid JWT format |
| `refreshToken` | string | Yes | Opaque refresh token | Non-empty string |
| `expiresIn` | number | Yes | Token lifetime in seconds | > 0 |
| `tokenType` | string | Yes | Token type (always "Bearer") | "Bearer" |

#### Validation Rules

```typescript
interface AuthenticationToken {
  accessToken: string;   // Must be valid JWT (3 base64 parts separated by dots)
  refreshToken: string;  // Must be non-empty
  expiresIn: number;     // Must be positive integer
  tokenType: "Bearer";   // Must be literal "Bearer"
}

// Validation
function validateAuthenticationToken(token: AuthenticationToken): ValidationResult {
  if (!isValidJwt(token.accessToken))
    return invalid("accessToken must be valid JWT");

  if (token.refreshToken.trim().length === 0)
    return invalid("refreshToken cannot be empty");

  if (token.expiresIn <= 0)
    return invalid("expiresIn must be positive");

  if (token.tokenType !== "Bearer")
    return invalid("tokenType must be 'Bearer'");

  return valid();
}
```

#### Security Constraints

1. **Access Token**:
   - MUST be transmitted over HTTPS only
   - MUST be stored in memory on frontend (never localStorage)
   - MUST be validated on every backend request (NFR-002)
   - MUST NOT be logged or exposed in error messages

2. **Refresh Token**:
   - MUST be stored in HttpOnly cookie (NFR-003)
   - MUST have `Secure` flag (HTTPS only)
   - MUST have `SameSite=Strict` flag (CSRF protection)
   - MUST be rotated on each refresh (defense in depth)

#### Backend Representation (C#)

```csharp
public class AuthenticationToken
{
    [Required]
    public string AccessToken { get; set; } = string.Empty;

    [Required]
    public string RefreshToken { get; set; } = string.Empty;

    [Required]
    [Range(1, int.MaxValue)]
    public int ExpiresIn { get; set; }

    [Required]
    public string TokenType { get; set; } = "Bearer";

    // Security: Never serialize refresh token to client
    [JsonIgnore]
    public string RefreshTokenForBackendOnly => RefreshToken;
}
```

---

### 3. AuthenticationResult

Represents the outcome of an authentication attempt.

**Source Requirements**: FR-012

#### Fields

| Field | Type | Required | Description | Constraints |
|-------|------|----------|-------------|-------------|
| `success` | boolean | Yes | Whether authentication succeeded | true/false |
| `userSession` | UserSession? | Conditional | User session (if success=true) | Required if success=true |
| `errorCode` | string? | Conditional | Error code (if success=false) | Required if success=false |
| `errorMessage` | string? | Conditional | User-friendly error message (if success=false) | Required if success=false |

#### Validation Rules

```typescript
interface AuthenticationResult {
  success: boolean;
  userSession?: UserSession;
  errorCode?: string;
  errorMessage?: string;
}

// Validation
function validateAuthenticationResult(result: AuthenticationResult): ValidationResult {
  if (result.success) {
    if (!result.userSession)
      return invalid("userSession required when success=true");
    if (result.errorCode || result.errorMessage)
      return invalid("errorCode/errorMessage must be null when success=true");
  } else {
    if (result.userSession)
      return invalid("userSession must be null when success=false");
    if (!result.errorCode || !result.errorMessage)
      return invalid("errorCode and errorMessage required when success=false");
  }
  return valid();
}
```

#### Error Code Mapping

| Entra ID Error | errorCode | errorMessage |
|----------------|-----------|--------------|
| `AADSTS50058` | `INTERACTION_REQUIRED` | "Please sign in again" |
| `AADSTS65001` | `USER_CANCELLED` | "Sign-in was cancelled. Please try again." |
| `AADSTS70011` | `INVALID_SCOPE` | "Configuration error. Please contact support." |
| `AADSTS50076` | `MFA_REQUIRED` | "Additional verification required" |
| Network error | `NETWORK_ERROR` | "Unable to connect. Please check your internet connection." |
| Token exchange failed | `TOKEN_EXCHANGE_FAILED` | "Authentication failed. Please try again." |
| State mismatch | `CSRF_DETECTED` | "Invalid request. Please start sign-in again." |

#### Backend Representation (C#)

```csharp
public class AuthenticationResult
{
    [Required]
    public bool Success { get; set; }

    public UserSession? UserSession { get; set; }

    public string? ErrorCode { get; set; }

    public string? ErrorMessage { get; set; }

    // Factory methods
    public static AuthenticationResult SuccessResult(UserSession session)
    {
        return new AuthenticationResult
        {
            Success = true,
            UserSession = session
        };
    }

    public static AuthenticationResult FailureResult(string errorCode, string errorMessage)
    {
        return new AuthenticationResult
        {
            Success = false,
            ErrorCode = errorCode,
            ErrorMessage = errorMessage
        };
    }
}
```

---

## Entity Relationships

```
User navigates to protected page
           │
           ▼
┌─────────────────────┐
│ Check UserSession   │
│ isAuthenticated?    │
└──────────┬──────────┘
           │
    ┌──────┴──────┐
    │             │
   YES            NO
    │             │
    │             ▼
    │      ┌──────────────┐
    │      │ Redirect to  │
    │      │ Login Page   │
    │      └──────┬───────┘
    │             │
    │             ▼
    │      ┌──────────────┐
    │      │ User clicks  │
    │      │ "Sign in"    │
    │      └──────┬───────┘
    │             │
    │             ▼
    │      ┌──────────────┐
    │      │ Entra ID     │
    │      │ Authentication│
    │      └──────┬───────┘
    │             │
    │             ▼
    │      ┌──────────────┐
    │      │ Callback:    │
    │      │ Create       │
    │      │ AuthToken    │
    │      └──────┬───────┘
    │             │
    │             ▼
    │      ┌──────────────┐
    │      │ Extract      │
    │      │ UserSession  │
    │      │ from JWT     │
    │      └──────┬───────┘
    │             │
    └─────────────┘
           │
           ▼
    ┌──────────────┐
    │ Allow access │
    │ to protected │
    │ resource     │
    └──────────────┘
```

---

## Data Flow

### 1. Login Flow

```
Frontend                     Backend                    Entra ID
   │                            │                           │
   │─────GET /auth/login────────>│                           │
   │                            │──────Redirect to────────>│
   │                            │      Entra ID login       │
   │<───────────────────────────│                           │
   │                                                        │
   │                         User authenticates with Entra  │
   │                                                        │
   │<───────────Callback with code & state─────────────────│
   │                            │                           │
   │──GET /auth/callback?code=X─>│                           │
   │                            │──Exchange code for────────>│
   │                            │      tokens               │
   │                            │<──AuthenticationToken─────│
   │                            │                           │
   │                            │ Extract UserSession       │
   │                            │ from JWT claims           │
   │                            │                           │
   │                            │ Store refresh token       │
   │                            │ in HttpOnly cookie        │
   │                            │                           │
   │<───AuthenticationResult────│                           │
   │    (success + userSession) │                           │
   │                            │                           │
   │ Store UserSession          │                           │
   │ in React context           │                           │
```

### 2. Token Refresh Flow

```
Frontend                     Backend                    Entra ID
   │                            │                           │
   │ Token expiring in 5 min    │                           │
   │                            │                           │
   │───POST /auth/refresh───────>│                           │
   │   (cookie: refresh_token)  │                           │
   │                            │─────Refresh token─────────>│
   │                            │                           │
   │                            │<──New access token────────│
   │                            │                           │
   │<───New access token────────│                           │
   │                            │                           │
   │ Update UserSession         │                           │
   │ accessTokenExpiration      │                           │
```

---

## Testing Considerations

### Unit Tests

1. **UserSession Validation**:
   - ✓ Valid session with all required fields
   - ✗ Missing userId
   - ✗ Invalid email format
   - ✗ Expired accessTokenExpiration
   - ✗ Invalid avatar URL (HTTP instead of HTTPS)

2. **AuthenticationToken Validation**:
   - ✓ Valid JWT access token
   - ✗ Invalid JWT format (wrong number of parts)
   - ✗ Empty refresh token
   - ✗ Negative expiresIn
   - ✗ Wrong tokenType

3. **AuthenticationResult Validation**:
   - ✓ Success result with userSession
   - ✓ Failure result with errorCode + errorMessage
   - ✗ Success result without userSession
   - ✗ Failure result without error details

### Integration Tests

1. **Login Flow**:
   - Given unauthenticated user
   - When complete Entra ID authentication
   - Then UserSession created with valid fields

2. **Token Refresh**:
   - Given expired access token
   - When POST /auth/refresh with valid refresh token
   - Then new access token returned, UserSession updated

3. **Logout**:
   - Given authenticated user
   - When POST /auth/logout
   - Then UserSession.isAuthenticated = false, cookies cleared

---

## Performance Considerations

| Operation | Target | Measurement |
|-----------|--------|-------------|
| Extract UserSession from JWT | <5ms | Backend middleware latency |
| Validate JWT signature | <2ms | Token validation latency |
| Refresh token exchange | <200ms | Network call to Entra ID |
| Serialize UserSession to JSON | <1ms | API response overhead |

---

## Security Audit Checklist

- [ ] Access tokens never stored in localStorage/sessionStorage
- [ ] Refresh tokens always in HttpOnly cookies
- [ ] Tokens never logged (even in debug mode)
- [ ] JWT signature validated on every request
- [ ] Token expiration strictly enforced
- [ ] State parameter validated to prevent CSRF
- [ ] HTTPS enforced for all authentication endpoints
- [ ] User input (name, email) sanitized before display (XSS prevention)

---

**Created by**: Claude Code | **Reviewed**: Pending | **Approved**: Pending

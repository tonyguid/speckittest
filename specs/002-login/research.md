# Research & Technical Decisions: User Authentication (Login)

**Feature**: 002-login | **Date**: January 20, 2026 | **Plan**: [plan.md](plan.md)

This document captures research findings and technical decisions made during Phase 0 of the implementation plan.

---

## Decision 1: MSAL Library Selection for React

**Question**: Should we use @azure/msal-react or @azure/msal-browser for Entra ID integration?

### Decision: `@azure/msal-react` (version ^3.0.0)

### Rationale

- **React Integration**: Provides React-specific hooks (`useMsal`, `useAccount`, `useIsAuthenticated`) and components (`MsalProvider`, `AuthenticatedTemplate`, `UnauthenticatedTemplate`)
- **TypeScript Support**: Full TypeScript definitions included
- **Built on msal-browser**: @azure/msal-react is a wrapper around @azure/msal-browser, providing the same functionality with React-friendly abstractions
- **Official Microsoft Library**: Actively maintained by Microsoft Identity team
- **PKCE Support**: Built-in support for OAuth 2.0 with PKCE (required by FR-003)
- **Token Caching**: Automatic token caching and refresh handling
- **SSO Support**: Silent sign-in support for users already authenticated with Microsoft

### Alternatives Considered

| Alternative | Reason for Rejection |
|-------------|---------------------|
| @azure/msal-browser | Lower-level API; requires manual React integration. @azure/msal-react provides better developer experience with hooks and context. |
| Custom OAuth implementation | High security risk, reinventing the wheel. Microsoft's library is battle-tested and handles edge cases. |
| oidc-client-ts | Generic OIDC library not optimized for Entra ID. Missing Entra-specific features like B2B guest support. |

### Security Implications

✅ **Positive**:
- PKCE enabled by default (mitigates authorization code interception)
- Automatic token validation
- Built-in protection against common OAuth vulnerabilities
- Regular security updates from Microsoft

⚠️ **Considerations**:
- Must configure secure redirect URIs
- Requires HTTPS in production
- Need to set appropriate scopes (avoid over-permissioning)

### Performance Implications

- **Bundle size**: ~130KB gzipped (acceptable for authentication library)
- **Silent token refresh**: Reduces authentication overhead for returning users
- **Token caching**: Minimizes network calls for token retrieval
- **Lazy loading**: Can be code-split if needed to improve initial page load

### Implementation Notes

```typescript
// Package installation
npm install @azure/msal-react @azure/msal-browser

// Basic configuration
import { PublicClientApplication } from "@azure/msal-browser";
import { MsalProvider } from "@azure/msal-react";

const msalConfig = {
  auth: {
    clientId: process.env.REACT_APP_ENTRA_CLIENT_ID,
    authority: `https://login.microsoftonline.com/${process.env.REACT_APP_ENTRA_TENANT_ID}`,
    redirectUri: process.env.REACT_APP_REDIRECT_URI,
  },
  cache: {
    cacheLocation: "memory", // See Decision 2
    storeAuthStateInCookie: false,
  },
};
```

---

## Decision 2: Token Storage Strategy

**Question**: Where should access tokens be stored on the frontend? (memory vs sessionStorage vs localStorage)

### Decision: In-Memory Storage for Access Tokens

### Rationale

- **Security First**: Memory storage prevents XSS attacks from accessing tokens (tokens cleared on page refresh)
- **Refresh Token in HttpOnly Cookie**: Backend sets refresh token in HttpOnly cookie (immune to XSS)
- **Short-Lived Access Tokens**: Access tokens expire quickly (e.g., 1 hour); refresh tokens handle session persistence
- **OWASP Recommendation**: Aligns with OWASP guidance for SPA token storage
- **Microsoft Best Practice**: Recommended approach in MSAL.js documentation

### Alternatives Considered

| Alternative | Reason for Rejection |
|-------------|---------------------|
| localStorage | Vulnerable to XSS attacks. Any malicious script can access localStorage. High security risk. |
| sessionStorage | Still vulnerable to XSS. Only marginally better than localStorage (cleared on tab close). |
| IndexedDB | Same XSS vulnerability as sessionStorage/localStorage. Unnecessary complexity. |

### Security Implications

✅ **Positive**:
- XSS attacks cannot steal access tokens (tokens not accessible via JavaScript after page refresh)
- Refresh token protected by HttpOnly cookie (cannot be accessed by JavaScript at all)
- Minimizes attack surface for token theft

⚠️ **Trade-offs**:
- User must re-authenticate after page refresh **IF** refresh token is also in memory
- **Mitigation**: Use HttpOnly cookie for refresh token → silent re-authentication on page load

### Performance Implications

- **Page Refresh**: Requires token refresh on every page load (adds ~100-200ms latency)
- **Mitigation**: Silent token acquisition using refresh token is fast
- **Memory Usage**: Minimal (<1KB for JWT tokens)

### Implementation Strategy

```typescript
// MSAL configuration
const msalConfig = {
  cache: {
    cacheLocation: "memory", // Access tokens in memory
    storeAuthStateInCookie: false, // Refresh tokens handled by backend HttpOnly cookie
  },
};

// Backend sets refresh token as HttpOnly cookie
// POST /auth/callback response:
Set-Cookie: refresh_token=<token>; HttpOnly; Secure; SameSite=Strict; Path=/auth/refresh
```

### User Experience Impact

- **Seamless for Active Users**: Silent token refresh happens automatically
- **Page Refresh**: <1 second delay for silent re-authentication (acceptable per NFR-001)
- **Session Persistence**: Survives page refreshes via refresh token cookie

---

## Decision 3: Backend Token Validation Approach

**Question**: Should we validate access tokens on every request or cache validation results?

### Decision: Validate on Every Request (No Caching)

### Rationale

- **Security Requirement**: NFR-002 explicitly states "Access tokens MUST be validated on every API request"
- **Token Revocation**: Ensures revoked tokens are immediately rejected
- **Stateless Architecture**: Maintains stateless API design (no server-side session storage)
- **Performance is Acceptable**: JWT validation is cryptographically fast (~1-2ms per request)
- **Scale Consideration**: Caching adds complexity for minimal performance gain at current scale

### Alternatives Considered

| Alternative | Reason for Rejection |
|-------------|---------------------|
| Cache validation for 5 minutes | Violates NFR-002. Creates window for revoked tokens to be accepted. Security risk. |
| Validate once per session | Violates stateless design. Requires server-side session storage. Adds operational complexity. |
| Client-side only validation | Insecure. Client validation can be bypassed. Backend must always validate. |

### Security Implications

✅ **Positive**:
- Immediate enforcement of token revocation
- No window for compromised tokens to be used after revocation
- Aligns with zero-trust security model
- Prevents replay attacks with expired tokens

### Performance Implications

- **Validation Cost**: 1-2ms per request (JWT signature verification + expiration check)
- **Total Overhead**: <5ms including claim extraction
- **Scale**: At 1000 req/s, adds ~2-5 CPU cores worth of work (acceptable for current infrastructure)
- **Optimization**: Can use Azure AD's public key caching to speed up signature verification

### Implementation Notes

```csharp
// Middleware configuration in Program.cs
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.Authority = $"https://login.microsoftonline.com/{tenantId}";
        options.Audience = clientId;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true, // Validates expiration on every request
            ValidateIssuerSigningKey = true,
            ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 min clock skew
        };
    });

// Applied to all controllers via [Authorize] attribute
[Authorize]
[ApiController]
[Route("api/[controller]")]
public class BlobController : ControllerBase { }
```

---

## Decision 4: Session Refresh Timing

**Question**: When should automatic token refresh be triggered? (e.g., 5 minutes before expiration, on-demand, etc.)

### Decision: Proactive Refresh at 5 Minutes Before Expiration

### Rationale

- **Spec Requirement**: SC-004 states "User sessions are automatically refreshed at least 5 minutes before token expiration"
- **Prevents Mid-Request Expiration**: Reduces risk of token expiring during long-running requests
- **User Experience**: Users never see authentication errors during active sessions
- **Network Reliability**: 5-minute buffer accounts for network latency and retries
- **MSAL Built-In Support**: @azure/msal-react automatically handles silent token refresh

### Alternatives Considered

| Alternative | Reason for Rejection |
|-------------|---------------------|
| On-demand (when request fails) | Poor UX. Users see errors during active sessions. Violates SC-004. |
| 1 minute before expiration | Too aggressive. May cause unnecessary refresh calls. 5 min is industry standard. |
| 10 minutes before expiration | Wastes token lifetime. Increases refresh API load unnecessarily. |
| No automatic refresh | Violates FR-009. Users forced to re-authenticate every hour. Terrible UX. |

### Security Implications

✅ **Positive**:
- Maintains continuous authentication state
- Reduces attack window for token theft (tokens expire faster)
- Refresh tokens can be rotated on each refresh (defense in depth)

⚠️ **Considerations**:
- Refresh token must be protected (HttpOnly cookie)
- Failed refresh should force re-authentication (handled gracefully)

### Performance Implications

- **API Load**: One refresh request per user per hour (minimal)
- **Latency**: 100-200ms for refresh request (non-blocking, happens in background)
- **Success Rate**: SC-004 metric can be monitored via Application Insights

### Implementation Strategy

```typescript
// MSAL automatically handles this with acquireTokenSilent
const acquireAccessToken = async () => {
  const request = {
    scopes: ["User.Read"],
    account: accounts[0],
  };

  try {
    // MSAL checks expiration and refreshes if <5 min remaining
    const response = await instance.acquireTokenSilent(request);
    return response.accessToken;
  } catch (error) {
    if (error instanceof InteractionRequiredAuthError) {
      // Silent refresh failed, redirect to login
      await instance.acquireTokenRedirect(request);
    }
  }
};

// Backend refresh endpoint (fallback if MSAL silent refresh fails)
// POST /auth/refresh
[HttpPost("refresh")]
public async Task<IActionResult> RefreshToken()
{
    var refreshToken = Request.Cookies["refresh_token"];
    if (string.IsNullOrEmpty(refreshToken))
        return Unauthorized();

    var newAccessToken = await _authService.RefreshAccessToken(refreshToken);
    return Ok(new { accessToken = newAccessToken });
}
```

### Monitoring

Track these metrics in Application Insights:
- `auth.token.refresh.success` - Should be >99% per SC-004
- `auth.token.refresh.latency` - Should be <500ms p95
- `auth.token.expired_during_request` - Should be near zero

---

## Additional Research Findings

### 1. Entra ID Application Registration Requirements

**Required Configurations**:
- **Redirect URIs**:
  - Local: `http://localhost:3000/auth/callback`
  - Production: `https://<app-domain>/auth/callback`
- **Supported Account Types**: "Accounts in this organizational directory only" (single tenant)
- **Platform**: Single-page application (SPA) - enables PKCE
- **API Permissions**:
  - `User.Read` (delegated) - for user profile
  - `offline_access` (delegated) - for refresh tokens
  - `openid`, `profile`, `email` - standard OpenID Connect scopes

**Security Settings**:
- Enable "Access tokens" in Authentication > Implicit grant settings (for hybrid flow fallback)
- Configure token lifetime: Access token = 1 hour, Refresh token = 90 days
- Enable Conditional Access policies (optional, for MFA enforcement)

### 2. Avatar URL Extraction

**Finding**: Entra ID does not include avatar/photo URL in JWT claims by default.

**Solution**:
1. **Option A**: Call Microsoft Graph API `/me/photo/$value` after authentication (recommended)
   - Requires `User.Read` permission
   - Returns binary image data
   - Can be cached on backend

2. **Option B**: Use user initials as fallback avatar
   - Extract from `name` claim
   - Generate colored circle with initials (common UX pattern)

**Decision**: Implement Option A with Option B as fallback (if Graph API call fails or user has no photo).

### 3. Error Handling Patterns

**Common Entra ID Errors**:
| Error Code | Meaning | User Message | Recovery |
|------------|---------|--------------|----------|
| `AADSTS50058` | Silent sign-in failed (need interaction) | "Please sign in again" | Redirect to interactive login |
| `AADSTS65001` | User cancelled consent | "Sign-in was cancelled" | Offer retry button |
| `AADSTS70011` | Invalid scope requested | "Configuration error" | Log for DevOps investigation |
| `AADSTS50076` | MFA required | "Additional verification required" | Redirect to MFA flow |

**Implementation**: Map error codes to user-friendly messages in `ErrorPage.tsx`.

---

## Technology Stack Summary

### Frontend Dependencies
```json
{
  "dependencies": {
    "@azure/msal-react": "^3.0.0",
    "@azure/msal-browser": "^3.0.0",
    "react": "^18.2.0",
    "react-router-dom": "^6.20.0",
    "axios": "^1.6.0"
  },
  "devDependencies": {
    "@testing-library/react": "^14.1.0",
    "vitest": "^1.1.0",
    "@playwright/test": "^1.40.0"
  }
}
```

### Backend Dependencies
```xml
<ItemGroup>
  <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="9.0.0" />
  <PackageReference Include="Microsoft.Identity.Web" Version="2.15.0" />
  <PackageReference Include="Azure.Identity" Version="1.13.1" />
  <PackageReference Include="Microsoft.Graph" Version="5.35.0" /> <!-- For avatar retrieval -->
</ItemGroup>
```

### Infrastructure Requirements
- **Entra ID Tenant**: Required for application registration
- **HTTPS Certificate**: Required for production (HttpOnly cookies)
- **Application Insights**: For monitoring auth metrics (SC-001, SC-002, SC-004)

---

## Risks & Mitigations

| Risk | Likelihood | Impact | Mitigation |
|------|------------|--------|------------|
| MSAL library breaking change | Low | Medium | Pin to minor version (^3.0.0), test upgrades thoroughly |
| Entra ID outage | Low | High | Implement graceful degradation, show maintenance message |
| Token refresh race condition | Medium | Low | MSAL handles internally with mutex/locking |
| CORS issues with Entra ID | Medium | High | Configure redirect URIs correctly, test in staging first |
| Users without Entra ID account | Low | High | Clear error message, contact IT department instructions |

---

## Open Questions for Phase 1

1. **Avatar Caching Strategy**: How long should we cache user profile photos? (Recommendation: 24 hours)
2. **Multi-Tab Behavior**: Should logout in one tab log out all tabs? (Recommendation: Yes, use BroadcastChannel API)
3. **Remember Me Functionality**: Do we need "stay signed in" option? (Recommendation: No, enterprise users prefer re-auth)
4. **Session Timeout**: Should there be an inactivity timeout? (Recommendation: No, rely on token expiration only)

---

## Next Steps

✅ **Phase 0 Complete**: All research tasks resolved.

**Ready for Phase 1**:
1. Create `data-model.md` with entity definitions
2. Generate OpenAPI contracts in `contracts/` directory
3. Create `quickstart.md` with developer setup guide
4. Update agent context with selected technologies

**Estimated Phase 1 Duration**: 1-2 hours

---

**Researched by**: Claude Code | **Reviewed**: Pending | **Approved**: Pending

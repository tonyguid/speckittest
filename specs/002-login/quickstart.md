# Quickstart: User Authentication (Login) Development Setup

**Feature**: 002-login | **Date**: January 20, 2026 | **Plan**: [plan.md](plan.md)

This guide helps developers set up their local environment to work on the User Authentication feature.

---

## Prerequisites

### Required Tools
- [ ] .NET 10.0 SDK installed
- [ ] Node.js 18+ and npm installed
- [ ] Azure CLI installed (`az` command available)
- [ ] Access to the organization's Entra ID tenant
- [ ] Permissions to register applications in Entra ID (or contact DevOps team)

### Accounts
- [ ] Microsoft account with access to the organization's Entra ID tenant
- [ ] Azure subscription (for Application Insights, optional for local dev)

---

## Step 1: Register Application in Entra ID

### Option A: Manual Registration (Recommended for Learning)

1. **Navigate to Azure Portal**:
   ```
   https://portal.azure.com → Entra ID → App registrations → New registration
   ```

2. **Application Details**:
   - **Name**: `BlobCopy-Dev-{YourName}` (e.g., `BlobCopy-Dev-JohnDoe`)
   - **Supported account types**: "Accounts in this organizational directory only (Single tenant)"
   - **Redirect URI**:
     - Platform: Single-page application (SPA)
     - URI: `http://localhost:3000/auth/callback`

3. **Click "Register"** and note the following values:
   - **Application (client) ID**: (e.g., `a1b2c3d4-e5f6-7890-abcd-ef1234567890`)
   - **Directory (tenant) ID**: (e.g., `f6725568-0525-4454-88a6-bf4216fffc68`)

4. **Add Backend Redirect URI**:
   - Go to **Authentication** → **Add a platform** → **Web**
   - Redirect URI: `https://localhost:5001/auth/callback`
   - Check "Access tokens" and "ID tokens" under Implicit grant
   - Save

5. **Configure API Permissions**:
   - Go to **API permissions** → **Add a permission** → **Microsoft Graph** → **Delegated permissions**
   - Add these permissions:
     - `User.Read` (default, already added)
     - `offline_access` (for refresh tokens)
     - `openid`, `profile`, `email` (OpenID Connect scopes)
   - Click "Grant admin consent" (or request from admin)

6. **Generate Client Secret** (for backend):
   - Go to **Certificates & secrets** → **New client secret**
   - Description: "Development Secret"
   - Expires: 6 months
   - **Copy the secret value immediately** (it won't be shown again)

### Option B: Automated Registration (Using Azure CLI)

```bash
# Login to Azure
az login

# Set tenant ID
TENANT_ID="YOUR_TENANT_ID_HERE"

# Create app registration
APP_NAME="BlobCopy-Dev-$(whoami)"
az ad app create \
  --display-name "$APP_NAME" \
  --sign-in-audience AzureADMyOrg \
  --web-redirect-uris "https://localhost:5001/auth/callback" \
  --spa-redirect-uris "http://localhost:3000/auth/callback" \
  --required-resource-accesses @- <<EOF
[
  {
    "resourceAppId": "00000003-0000-0000-c000-000000000000",
    "resourceAccess": [
      { "id": "e1fe6dd8-ba31-4d61-89e7-88639da4683d", "type": "Scope" },
      { "id": "64a6cdd6-aab1-4aaf-94b8-3cc8405e90d0", "type": "Scope" },
      { "id": "14dad69e-099b-42c9-810b-d002981feec1", "type": "Scope" },
      { "id": "37f7f235-527c-4136-accd-4a02d197296e", "type": "Scope" },
      { "id": "7427e0e9-2fba-42fe-b0c0-848c9e6a8182", "type": "Scope" }
    ]
  }
]
EOF

# Get app ID
APP_ID=$(az ad app list --display-name "$APP_NAME" --query "[0].appId" -o tsv)
echo "Application ID: $APP_ID"

# Create client secret
az ad app credential reset --id $APP_ID --append --query password -o tsv
```

---

## Step 2: Configure Backend Environment

1. **Navigate to backend project**:
   ```bash
   cd src/backend/BlobCopy.API
   ```

2. **Create `appsettings.Development.json`** (if not exists):
   ```json
   {
     "Logging": {
       "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
       }
     },
     "EntraId": {
       "TenantId": "YOUR_TENANT_ID",
       "ClientId": "YOUR_CLIENT_ID",
       "ClientSecret": "YOUR_CLIENT_SECRET",
       "RedirectUri": "https://localhost:5001/auth/callback",
       "Scopes": ["User.Read", "offline_access"]
     },
     "Jwt": {
       "Authority": "https://login.microsoftonline.com/YOUR_TENANT_ID",
       "Audience": "YOUR_CLIENT_ID",
       "ValidIssuer": "https://login.microsoftonline.com/YOUR_TENANT_ID/v2.0"
     }
   }
   ```

3. **Replace placeholders**:
   - `YOUR_TENANT_ID`: Directory (tenant) ID from Step 1
   - `YOUR_CLIENT_ID`: Application (client) ID from Step 1
   - `YOUR_CLIENT_SECRET`: Client secret from Step 1

4. **Add to `.gitignore`** (if not already there):
   ```
   **/appsettings.Development.json
   ```

5. **Install NuGet packages** (if not already added):
   ```bash
   dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 9.0.0
   dotnet add package Microsoft.Identity.Web --version 2.15.0
   dotnet add package Microsoft.Graph --version 5.35.0
   ```

---

## Step 3: Configure Frontend Environment

1. **Navigate to frontend project**:
   ```bash
   cd ../../../src/frontend
   ```

2. **Create `.env.local`** file:
   ```env
   VITE_ENTRA_TENANT_ID=YOUR_TENANT_ID
   VITE_ENTRA_CLIENT_ID=YOUR_CLIENT_ID
   VITE_REDIRECT_URI=http://localhost:3000/auth/callback
   VITE_API_BASE_URL=https://localhost:5001
   ```

3. **Replace placeholders**:
   - `YOUR_TENANT_ID`: Directory (tenant) ID from Step 1
   - `YOUR_CLIENT_ID`: Application (client) ID from Step 1

4. **Add to `.gitignore`** (if not already there):
   ```
   .env.local
   ```

5. **Install npm packages**:
   ```bash
   npm install @azure/msal-react @azure/msal-browser
   ```

---

## Step 4: Run Backend

1. **Navigate to backend**:
   ```bash
   cd src/backend/BlobCopy.API
   ```

2. **Trust the development certificate** (first time only):
   ```bash
   dotnet dev-certs https --trust
   ```

3. **Run the backend**:
   ```bash
   dotnet run
   ```

4. **Verify backend is running**:
   - Open: `https://localhost:5001/swagger`
   - You should see the Swagger UI with authentication endpoints

---

## Step 5: Run Frontend

1. **Open a new terminal and navigate to frontend**:
   ```bash
   cd src/frontend
   ```

2. **Start the development server**:
   ```bash
   npm run dev
   ```

3. **Verify frontend is running**:
   - Open: `http://localhost:3000`
   - You should see the Blob Copy application

---

## Step 6: Test Authentication Flow

### End-to-End Test

1. **Navigate to the app**:
   ```
   http://localhost:3000
   ```

2. **Expected behavior**:
   - Since you're not authenticated, you should be redirected to `/login`
   - You should see a "Sign in with Microsoft" button

3. **Click "Sign in with Microsoft"**:
   - You'll be redirected to `login.microsoftonline.com`
   - Enter your organization credentials
   - If prompted, consent to the requested permissions

4. **After successful authentication**:
   - You should be redirected back to `http://localhost:3000/blob-copy`
   - You should see your name, email, and avatar in the header
   - You should see a "Logout" button

5. **Test logout**:
   - Click "Logout"
   - You should be redirected to `/login`
   - You should no longer be authenticated

### Verify Token Refresh

1. **Open browser DevTools** (F12) → **Network** tab

2. **Filter for** `refresh` requests

3. **Wait 55 minutes** (or modify token expiration for faster testing)

4. **Expected behavior**:
   - You should see a `POST /auth/refresh` request
   - The request should succeed with a new access token
   - You should remain logged in without any user interaction

---

## Step 7: Run Tests

### Backend Tests

```bash
cd src/backend/BlobCopy.Tests
dotnet test
```

**Expected**: All authentication service tests should pass.

### Frontend Tests

```bash
cd src/frontend
npm run test
```

**Expected**: All auth hook and component tests should pass.

### E2E Tests

```bash
cd src/frontend
npm run test:e2e
```

**Expected**: Full authentication flow E2E test should pass.

---

## Troubleshooting

### Issue: "AADSTS50011: The reply URL specified in the request does not match..."

**Solution**:
- Verify redirect URIs in Entra ID app registration match exactly
- Check for trailing slashes (should not have them)
- Ensure protocol matches (http vs https)

### Issue: "CORS error when calling backend"

**Solution**:
- Verify backend CORS policy allows `http://localhost:3000`
- Check that frontend is using correct API base URL in `.env.local`

### Issue: "Token validation failed"

**Solution**:
- Verify `TenantId` and `ClientId` in `appsettings.Development.json` are correct
- Ensure JWT `Authority` and `Audience` are set correctly
- Check that system clock is synchronized (JWT expiration is time-sensitive)

### Issue: "Cannot fetch user avatar"

**Solution**:
- Verify `User.Read` permission is granted and consented
- Check that user has a profile photo in Microsoft 365
- Fallback to initials avatar should still work

### Issue: "Refresh token cookie not set"

**Solution**:
- Verify backend is running on HTTPS (`https://localhost:5001`)
- Check that `SameSite`, `HttpOnly`, `Secure` flags are set correctly
- Ensure frontend and backend are on different ports (cookie domain rules)

---

## Development Workflow

### Daily Workflow

1. **Start backend**: `dotnet run` (in `src/backend/BlobCopy.API`)
2. **Start frontend**: `npm run dev` (in `src/frontend`)
3. **Run tests** after making changes
4. **Commit often** with descriptive messages

### Before Committing

- [ ] All tests pass (`dotnet test` + `npm run test`)
- [ ] No hardcoded secrets in code
- [ ] `appsettings.Development.json` and `.env.local` are in `.gitignore`
- [ ] Code follows project style guidelines

### Before Opening PR

- [ ] All acceptance criteria from spec.md are met
- [ ] E2E tests pass (`npm run test:e2e`)
- [ ] Manual testing completed
- [ ] Documentation updated (if needed)

---

## Useful Commands

### Backend

```bash
# Run with hot reload
dotnet watch run

# Run specific test
dotnet test --filter "FullyQualifiedName~AuthenticationServiceTests"

# Generate code coverage
dotnet test /p:CollectCoverage=true
```

### Frontend

```bash
# Run tests in watch mode
npm run test -- --watch

# Run specific test file
npm run test -- auth.test.ts

# Generate coverage report
npm run test:coverage
```

### Azure CLI

```bash
# Get access token for testing
az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798

# View app registration details
az ad app show --id YOUR_CLIENT_ID

# List app permissions
az ad app permission list --id YOUR_CLIENT_ID
```

---

## Next Steps

Once your environment is set up:

1. Review [plan.md](plan.md) for implementation approach
2. Review [data-model.md](data-model.md) for entity definitions
3. Review API contracts in [contracts/](contracts/) directory
4. Start implementing tasks from `tasks.md` (when created)
5. Reference [research.md](research.md) for technical decisions

---

## Additional Resources

- **MSAL.js Documentation**: https://learn.microsoft.com/en-us/entra/identity-platform/msal-overview
- **Microsoft Identity Web**: https://learn.microsoft.com/en-us/entra/msal/dotnet/microsoft-identity-web/
- **OAuth 2.0 + PKCE Flow**: https://oauth.net/2/pkce/
- **Microsoft Graph API**: https://learn.microsoft.com/en-us/graph/use-the-api
- **JWT Debugging**: https://jwt.ms (decode tokens to inspect claims)

---

**Created by**: Claude Code | **Last Updated**: January 20, 2026

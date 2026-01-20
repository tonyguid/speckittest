# Phase 1 Setup - Remaining Steps

## ✅ Completed
- T001: Backend NuGet packages installed (Microsoft.AspNetCore.Authentication.JwtBearer, Microsoft.Identity.Web, Microsoft.Graph)
- T002: Frontend npm packages installed (@azure/msal-react, @azure/msal-browser)

## 📋 Manual Steps Required (T003-T005)

### T003: Create Entra ID Application Registration

**Follow the detailed instructions in**: [specs/002-login/quickstart.md](specs/002-login/quickstart.md#step-1-register-application-in-entra-id)

**Quick Summary**:
1. Navigate to Azure Portal → Entra ID → App registrations → New registration
2. Name: `BlobCopy-Dev-{YourName}`
3. Supported account types: Single tenant
4. Redirect URIs:
   - Platform: Single-page application (SPA)
   - URI: `http://localhost:3000/auth/callback`
5. Add backend redirect URI:
   - Platform: Web
   - URI: `https://localhost:5001/auth/callback`
6. Configure API permissions:
   - User.Read (delegated)
   - offline_access (delegated)
   - openid, profile, email
7. Grant admin consent
8. Create client secret (save immediately - won't be shown again!)

**Save these values**:
- Tenant ID: `___________________________________`
- Client ID: `___________________________________`
- Client Secret: `___________________________________`

### T004: Configure Backend Settings

1. Copy the template file:
   ```bash
   cp src/backend/BlobCopy.API/appsettings.Development.json.template \
      src/backend/BlobCopy.API/appsettings.Development.json
   ```

2. Edit `src/backend/BlobCopy.API/appsettings.Development.json`:
   - Replace `YOUR_TENANT_ID_HERE` with your Tenant ID (3 places)
   - Replace `YOUR_CLIENT_ID_HERE` with your Client ID (2 places)
   - Replace `YOUR_CLIENT_SECRET_HERE` with your Client Secret

3. Verify the file is in `.gitignore`:
   ```bash
   grep "appsettings.Development.json" .gitignore
   ```

### T005: Configure Frontend Environment

1. Copy the template file:
   ```bash
   cp src/frontend/.env.local.template src/frontend/.env.local
   ```

2. Edit `src/frontend/.env.local`:
   - Replace `YOUR_TENANT_ID_HERE` with your Tenant ID
   - Replace `YOUR_CLIENT_ID_HERE` with your Client ID

3. Verify the file is in `.gitignore`:
   ```bash
   grep ".env.local" .gitignore
   ```

## ✅ Verification

After completing the manual steps, verify Phase 1 is complete:

```bash
# Check backend configuration exists
ls src/backend/BlobCopy.API/appsettings.Development.json

# Check frontend configuration exists
ls src/frontend/.env.local

# Verify packages are installed
cd src/backend/BlobCopy.API && dotnet list package | grep Microsoft.Identity.Web
cd ../../frontend && npm list @azure/msal-react
```

## 🎯 Next Steps

Once T003-T005 are complete, you're ready for Phase 2 (Foundational tasks):
- Create models (UserSession, AuthenticationToken, AuthenticationResult)
- Configure JWT authentication
- Set up MSAL context

See: [specs/002-login/tasks.md](specs/002-login/tasks.md#phase-2-foundational-blocking-prerequisites)

$ErrorActionPreference = "Stop"

$organization = "qcellsces"
$project = "test-project-tony"

# Get Azure AD token for Azure DevOps
Write-Host "Getting Azure AD token for Azure DevOps..."
$tokenResponse = az account get-access-token --resource 499b84ac-1321-427f-aa17-267ca6975798 --query accessToken -o tsv

if (-not $tokenResponse) {
    Write-Error "Failed to get Azure AD token. Make sure you're logged in with 'az login'"
    exit 1
}

$headers = @{
    "Authorization" = "Bearer $tokenResponse"
    "Content-Type" = "application/json-patch+json"
}

$description = @"
The system requires foundational infrastructure, configuration, and environment preparation to support all upcoming functionality. This includes establishing the project structure, initializing required services, configuring authentication and access, and ensuring that the application can reliably run and interact with its dependencies before any user-facing features are implemented.

**Why this priority**: Without a stable and validated foundation, no user-facing capabilities can function. This work is essential, non-negotiable, and must be completed before any feature can deliver value. It ensures the system is secure, operational, and ready for core functionality.

**Independent Test**: Can be fully tested by verifying that the environment builds successfully, required services are reachable, authentication infrastructure is configured, and a basic operational check confirms the system is ready for higher-level features.

**Acceptance Scenarios**:

1. **Given** the development environment is initialized, **When** the system builds and starts, **Then** all foundational services, libraries, and configurations load without errors
2. **Given** the system is running, **When** it attempts to configure authentication using Entra ID (application registration, environment variables), **Then** the configuration is valid and the system can obtain access to required resources
3. **Given** the foundational setup is complete, **When** the system performs a basic operational check (backend runs on HTTPS, frontend connects to backend), **Then** the system confirms that required dependencies are reachable and permissions are correctly configured
4. **Given** the foundational environment is configured, **When** JWT authentication middleware is initialized and MSAL context is established, **Then** the system successfully validates the setup, demonstrating readiness for user-facing authentication features
"@

$body = @(
    @{ op = "add"; path = "/fields/System.Title"; value = "Foundational Setup for System Operations" }
    @{ op = "add"; path = "/fields/System.WorkItemType"; value = "User Story" }
    @{ op = "add"; path = "/fields/System.State"; value = "New" }
    @{ op = "add"; path = "/fields/System.Tags"; value = "Speckit" }
    @{ op = "add"; path = "/fields/System.Description"; value = $description }
    @{ op = "add"; path = "/relations/-"; value = @{
        rel = "System.LinkTypes.Hierarchy-Reverse"
        url = "https://dev.azure.com/$organization/$project/_apis/wit/workItems/3058"
    }}
)

$url = "https://dev.azure.com/$organization/$project/_apis/wit/workitems/`$User Story?api-version=7.0"

Write-Host "Creating User Story 0 in Azure DevOps..."
Write-Host "URL: $url"

try {
    $response = Invoke-RestMethod -Uri $url -Method POST -Headers $headers -Body ($body | ConvertTo-Json -Depth 10)
    Write-Host "✅ Successfully created User Story 0"
    Write-Host "ADO ID: $($response.id)"
    Write-Host "URL: $($response._links.html.href)"

    # Output the ID for capture
    $response.id
} catch {
    Write-Host "❌ Failed to create User Story 0"
    Write-Host "Error: $_"
    Write-Host "Response: $($_.Exception.Response)"
    exit 1
}

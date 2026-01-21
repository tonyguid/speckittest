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

# US0 task IDs from mirror file (T001-T013, T060-T067)
$taskIds = @(3103, 3104, 3106, 3107, 3108, 3109, 3110, 3111, 3112, 3113, 3114, 3115, 3116, 3163, 3164, 3165, 3166, 3167, 3168, 3169, 3170)

$successCount = 0
$failCount = 0

foreach ($taskId in $taskIds) {
    Write-Host "`nUpdating Task $taskId to link to User Story 3211..."

    # Remove old parent link and add new one in single operation
    $body = @(
        @{
            op = "remove"
            path = "/relations/0"
        },
        @{
            op = "add"
            path = "/relations/-"
            value = @{
                rel = "System.LinkTypes.Hierarchy-Reverse"
                url = "https://dev.azure.com/$organization/$project/_apis/wit/workItems/3211"
            }
        }
    )

    $url = "https://dev.azure.com/$organization/$project/_apis/wit/workitems/$taskId`?api-version=7.0"

    try {
        $response = Invoke-RestMethod -Uri $url -Method PATCH -Headers $headers -Body ($body | ConvertTo-Json -Depth 10)
        Write-Host "  ✅ Successfully updated Task $taskId"
        $successCount++
    } catch {
        Write-Host "  ❌ Failed to update Task $taskId"
        Write-Host "  Error: $_"
        $failCount++
    }

    # Rate limiting
    Start-Sleep -Milliseconds 200
}

Write-Host "`n========================================="
Write-Host "Update complete!"
Write-Host "  ✅ Success: $successCount"
Write-Host "  ❌ Failed: $failCount"
Write-Host "========================================="

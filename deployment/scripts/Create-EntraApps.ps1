# Create Entra ID Applications Script (PowerShell)
# This script creates the three Entra ID applications with proper permissions for Azure Storage access

param(
    [Parameter(Mandatory = $true)]
    [string]$TenantId,
    
    [Parameter(Mandatory = $false)]
    [string]$ApplicationNamePrefix = "proximity"
)

# Ensure we're connected to Azure AD
Write-Host "Connecting to Microsoft Graph..." -ForegroundColor Green
Connect-MgGraph -Scopes "Application.ReadWrite.All" -TenantId $TenantId

# Define the applications
$applications = @(
    @{
        Name        = "$ApplicationNamePrefix-storage-reader"
        Description = "Application for read-only access to Proximity storage account tables and blobs"
        Permissions = @("https://storage.azure.com/user_impersonation")
    },
    @{
        Name        = "$ApplicationNamePrefix-storage-writer"
        Description = "Application for write access to Proximity storage account tables and blobs"
        Permissions = @("https://storage.azure.com/user_impersonation")
    },
    @{
        Name        = "$ApplicationNamePrefix-storage-contributor"
        Description = "Application for full contributor access to Proximity storage account"
        Permissions = @("https://storage.azure.com/user_impersonation")
    }
)

# Storage Resource App ID
$storageResourceAppId = "e406a681-f3d4-42a8-90b6-c2b029497af1"

foreach ($app in $applications) {
    try {
        Write-Host "Creating application: $($app.Name)" -ForegroundColor Yellow
        
        # Check if app already exists
        $existingApp = Get-MgApplication -Filter "displayName eq '$($app.Name)'" -ErrorAction SilentlyContinue
        
        if ($existingApp) {
            Write-Host "Application '$($app.Name)' already exists with App ID: $($existingApp.AppId)" -ForegroundColor Blue
            continue
        }
        
        # Define required resource access for Azure Storage
        $requiredResourceAccess = @(
            @{
                ResourceAppId  = $storageResourceAppId
                ResourceAccess = @(
                    @{
                        Id   = "03e0da56-190b-40ad-a80c-ea378c433f7f"  # user_impersonation
                        Type = "Scope"
                    }
                )
            }
        )
        
        # Create the application
        $newApp = New-MgApplication -DisplayName $app.Name -Description $app.Description -SignInAudience "AzureADMyOrg" -RequiredResourceAccess $requiredResourceAccess
        
        # Create service principal for the application
        $servicePrincipal = New-MgServicePrincipal -AppId $newApp.AppId -DisplayName $app.Name
        
        Write-Host "✓ Created application '$($app.Name)'" -ForegroundColor Green
        Write-Host "  App ID: $($newApp.AppId)" -ForegroundColor Cyan
        Write-Host "  Object ID: $($newApp.Id)" -ForegroundColor Cyan
        Write-Host "  Service Principal ID: $($servicePrincipal.Id)" -ForegroundColor Cyan
        Write-Host ""
        
    }
    catch {
        Write-Error "Failed to create application '$($app.Name)': $_"
    }
}

Write-Host "Application creation completed!" -ForegroundColor Green
Write-Host ""
Write-Host "Next steps:" -ForegroundColor Yellow
Write-Host "1. Grant admin consent for the API permissions in the Azure Portal" -ForegroundColor White
Write-Host "2. Deploy your Bicep template to create managed identities and RBAC assignments" -ForegroundColor White
Write-Host "3. If you prefer to use these Entra applications instead of managed identities," -ForegroundColor White
Write-Host "   update your Bicep template to reference these application IDs" -ForegroundColor White
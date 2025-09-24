# Validate Storage Access Configuration
# This script validates that the managed identities and RBAC assignments are working correctly

param(
    [Parameter(Mandatory = $true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory = $true)]
    [string]$StorageAccountName
)

Write-Host "Validating Storage Access Configuration..." -ForegroundColor Green
Write-Host "Resource Group: $ResourceGroupName" -ForegroundColor Cyan
Write-Host "Storage Account: $StorageAccountName" -ForegroundColor Cyan
Write-Host ""

# Get managed identities
$identityNames = @(
    "proximity-storage-reader",
    "proximity-storage-writer", 
    "proximity-storage-contributor"
)

foreach ($identityName in $identityNames) {
    Write-Host "Checking managed identity: $identityName" -ForegroundColor Yellow
    
    try {
        $identity = az identity show --resource-group $ResourceGroupName --name $identityName --output json | ConvertFrom-Json
        
        if ($identity) {
            Write-Host "  ✓ Found managed identity" -ForegroundColor Green
            Write-Host "    Client ID: $($identity.clientId)" -ForegroundColor Cyan
            Write-Host "    Principal ID: $($identity.principalId)" -ForegroundColor Cyan
            
            # Check role assignments for this identity
            $storageAccountId = "/subscriptions/$((az account show --query id -o tsv))/resourceGroups/$ResourceGroupName/providers/Microsoft.Storage/storageAccounts/$StorageAccountName"
            $roleAssignments = az role assignment list --assignee $identity.principalId --scope $storageAccountId --output json | ConvertFrom-Json
            
            if ($roleAssignments.Count -gt 0) {
                Write-Host "    ✓ Role assignments found:" -ForegroundColor Green
                foreach ($assignment in $roleAssignments) {
                    Write-Host "      - $($assignment.roleDefinitionName)" -ForegroundColor White
                }
            }
            else {
                Write-Host "    ⚠ No role assignments found" -ForegroundColor Red
            }
        }
    }
    catch {
        Write-Host "  ✗ Failed to find managed identity: $identityName" -ForegroundColor Red
        Write-Host "    Error: $_" -ForegroundColor Red
    }
    
    Write-Host ""
}

# Check storage account configuration
Write-Host "Checking storage account configuration..." -ForegroundColor Yellow

try {
    $storageAccount = az storage account show --resource-group $ResourceGroupName --name $StorageAccountName --output json | ConvertFrom-Json
    
    Write-Host "  ✓ Storage Account: $($storageAccount.name)" -ForegroundColor Green
    Write-Host "    Shared Key Access: $($storageAccount.allowSharedKeyAccess)" -ForegroundColor Cyan
    Write-Host "    Public Access: $($storageAccount.publicNetworkAccess)" -ForegroundColor Cyan
    Write-Host "    HTTPS Only: $($storageAccount.supportsHttpsTrafficOnly)" -ForegroundColor Cyan
    
    if ($storageAccount.allowSharedKeyAccess -eq $false) {
        Write-Host "    ✓ Shared key access is properly disabled" -ForegroundColor Green
    }
    else {
        Write-Host "    ⚠ Shared key access is enabled (consider disabling for enhanced security)" -ForegroundColor Yellow
    }
    
}
catch {
    Write-Host "  ✗ Failed to retrieve storage account information" -ForegroundColor Red
    Write-Host "    Error: $_" -ForegroundColor Red
}

Write-Host ""
Write-Host "Validation completed!" -ForegroundColor Green
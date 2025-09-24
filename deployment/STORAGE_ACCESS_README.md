# Proximity Storage Access - Entra ID Applications and RBAC

This solution creates three types of storage access identities for the Proximity application with different permission levels:

## Access Levels

### 1. proximity-storage-reader
- **Permissions**: Read-only access
- **Azure Roles**:
  - `Storage Table Data Reader` - Read access to Azure Tables
  - `Storage Blob Data Reader` - Read access to Azure Blobs
- **Use Case**: Applications or services that only need to read data from storage

### 2. proximity-storage-writer  
- **Permissions**: Read/Write access to data
- **Azure Roles**:
  - `Storage Table Data Contributor` - Read/write access to Azure Tables
  - `Storage Blob Data Contributor` - Read/write access to Azure Blobs
- **Use Case**: Applications that need to read and write data but not manage the storage account itself

### 3. proximity-storage-contributor
- **Permissions**: Full storage account access
- **Azure Roles**:
  - `Contributor` - Full access to the storage account including management operations
- **Use Case**: Administrative applications or deployment processes that need full control

## Implementation Options

This solution provides **two implementation approaches**:

### Option 1: Managed Identities (Recommended)
- **What**: Uses Azure Managed Identities for secure, keyless authentication
- **Bicep Files**: 
  - `modules/entra-apps.bicep` - Creates user-assigned managed identities
  - `modules/rbac-assignments.bicep` - Assigns storage roles to managed identities
- **Advantages**: 
  - No credential management required
  - Automatic credential rotation
  - More secure than application registrations
  - Fully supported in Bicep/ARM templates

### Option 2: Entra ID Applications (Traditional)
- **What**: Creates traditional Entra ID application registrations
- **Scripts**: 
  - `scripts/Create-EntraApps.ps1` (PowerShell)
  - `scripts/create-entra-apps.sh` (Azure CLI)
- **Use Case**: When you need traditional app registrations for legacy applications or specific authentication flows

## Deployment Instructions

### Deploy Managed Identities (Option 1 - Recommended)

1. **Deploy the Bicep template**:
   ```bash
   az deployment group create \
     --resource-group <your-resource-group> \
     --template-file deployment/main.bicep \
     --parameters location="East US"
   ```

2. **Get the managed identity details**:
   ```bash
   # Get output values from the deployment
   az deployment group show \
     --resource-group <your-resource-group> \
     --name <deployment-name> \
     --query "properties.outputs"
   ```

3. **Use in your applications**:
   ```csharp
   // Example: Using managed identity in .NET
   var credential = new DefaultAzureCredential();
   var tableClient = new TableClient(
       new Uri("https://proxdata<suffix>.table.core.windows.net/"),
       "MyTable",
       credential);
   ```

### Create Entra Applications (Option 2)

1. **Using PowerShell**:
   ```powershell
   .\deployment\scripts\Create-EntraApps.ps1 -TenantId "your-tenant-id"
   ```

2. **Using Azure CLI**:
   ```bash
   ./deployment/scripts/create-entra-apps.sh "your-tenant-id"
   ```

3. **Grant admin consent** in Azure Portal:
   - Navigate to Azure AD > App registrations
   - Select each application
   - Go to API permissions
   - Click "Grant admin consent"

## Role Assignments Reference

| Identity | Table Access | Blob Access | Storage Account |
|----------|-------------|-------------|-----------------|
| proximity-storage-reader | Read Only | Read Only | No Access |
| proximity-storage-writer | Read/Write | Read/Write | No Access |
| proximity-storage-contributor | Full Access | Full Access | Full Access |

## Built-in Role IDs

The following Azure built-in role IDs are used:

- **Storage Table Data Reader**: `76199441-5572-49bb-9a17-0eb405e46d89`
- **Storage Blob Data Reader**: `2a2b9908-6ea1-4ae2-8e65-a410df84e7d1`
- **Storage Table Data Contributor**: `0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3`
- **Storage Blob Data Contributor**: `ba92f5b4-2d11-453d-a403-e96b0029c9fe`
- **Contributor**: `b24988ac-6180-42a0-ab88-20f7382dd24c`

## Security Best Practices

1. **Use Managed Identities** when possible instead of application registrations
2. **Apply principle of least privilege** - use the most restrictive role that meets requirements
3. **Disable shared key access** on storage accounts (already configured in storage.bicep)
4. **Use private endpoints** for storage access (already configured)
5. **Monitor access patterns** using Azure Monitor and Storage Analytics

## File Structure

```
deployment/
├── main.bicep                          # Main template with all modules
├── modules/
│   ├── entra-apps.bicep               # Managed identities creation
│   ├── rbac-assignments.bicep         # Role assignments
│   ├── storage.bicep                  # Storage account (existing)
│   └── ...                           # Other existing modules
└── scripts/
    ├── Create-EntraApps.ps1           # PowerShell script for Entra apps
    └── create-entra-apps.sh           # Bash script for Entra apps
```

## Outputs

After deployment, the following outputs are available:

### Managed Identity Outputs
- `readerManagedIdentityId` - Resource ID of reader managed identity
- `readerManagedIdentityClientId` - Client ID for authentication
- `writerManagedIdentityId` - Resource ID of writer managed identity  
- `writerManagedIdentityClientId` - Client ID for authentication
- `contributorManagedIdentityId` - Resource ID of contributor managed identity
- `contributorManagedIdentityClientId` - Client ID for authentication

### Role Assignment Outputs
- `readerTableRoleAssignmentId` - Role assignment ID for reader table access
- `readerBlobRoleAssignmentId` - Role assignment ID for reader blob access
- `writerTableRoleAssignmentId` - Role assignment ID for writer table access
- `writerBlobRoleAssignmentId` - Role assignment ID for writer blob access
- `contributorRoleAssignmentId` - Role assignment ID for contributor access

## Troubleshooting

### Common Issues

1. **Permission Denied**: Ensure the deployment principal has `User Access Administrator` or `Owner` role
2. **Role Assignment Fails**: Verify the managed identity was created successfully
3. **Application Auth Fails**: Check that admin consent was granted for Entra applications

### Verification Commands

```bash
# Check managed identity
az identity show --resource-group <rg> --name proximity-storage-reader

# Check role assignments
az role assignment list --assignee <principal-id> --scope <storage-account-id>

# Test storage access
az storage table list --account-name <storage-account> --auth-mode login
```
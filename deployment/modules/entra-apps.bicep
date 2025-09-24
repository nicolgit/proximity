// Entra ID Applications module - Creates application registrations and service principals
// for storage access with different permission levels

// Parameters
param applicationNamePrefix string = 'proximity'

// Note: Bicep doesn't natively support creating Entra ID applications.
// This module creates managed identities instead, which are more secure and easier to manage.
// Alternatively, you can create the applications via Azure CLI or PowerShell scripts.

// Reader User-Assigned Managed Identity - Read-only access to storage tables and blobs
resource readerManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${applicationNamePrefix}-storage-reader'
  location: resourceGroup().location
  tags: {
    purpose: 'Storage read-only access'
    scope: 'Tables and Blobs'
  }
}

// Writer User-Assigned Managed Identity - Write access to storage tables and blobs
resource writerManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${applicationNamePrefix}-storage-writer'
  location: resourceGroup().location
  tags: {
    purpose: 'Storage write access'
    scope: 'Tables and Blobs'
  }
}

// Contributor User-Assigned Managed Identity - Full access to storage account
resource contributorManagedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: '${applicationNamePrefix}-storage-contributor'
  location: resourceGroup().location
  tags: {
    purpose: 'Storage contributor access'
    scope: 'Full storage account'
  }
}

// Outputs
output readerManagedIdentityId string = readerManagedIdentity.id
output readerManagedIdentityPrincipalId string = readerManagedIdentity.properties.principalId
output readerManagedIdentityClientId string = readerManagedIdentity.properties.clientId
output readerManagedIdentityName string = readerManagedIdentity.name

output writerManagedIdentityId string = writerManagedIdentity.id
output writerManagedIdentityPrincipalId string = writerManagedIdentity.properties.principalId
output writerManagedIdentityClientId string = writerManagedIdentity.properties.clientId
output writerManagedIdentityName string = writerManagedIdentity.name

output contributorManagedIdentityId string = contributorManagedIdentity.id
output contributorManagedIdentityPrincipalId string = contributorManagedIdentity.properties.principalId
output contributorManagedIdentityClientId string = contributorManagedIdentity.properties.clientId
output contributorManagedIdentityName string = contributorManagedIdentity.name

// RBAC Assignments module - Assigns Azure storage roles to managed identities
// Based on the principle of least privilege access

// Parameters
param storageAccountId string
param readerPrincipalId string
param writerPrincipalId string
param contributorPrincipalId string

// Built-in Azure Role Definitions (these are the standard GUIDs for Azure built-in roles)
var storageTableDataReaderRoleId = '76199441-5572-49bb-9a17-0eb405e46d89'
var storageBlobDataReaderRoleId = '2a2b9908-6ea1-4ae2-8e65-a410df84e7d1'
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
var contributorRoleId = 'b24988ac-6180-42a0-ab88-20f7382dd24c'

// Get reference to the existing storage account for role assignment scoping
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: last(split(storageAccountId, '/'))
}

// Reader Role Assignments - Read-only access to tables and blobs
resource readerTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, readerPrincipalId, storageTableDataReaderRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageTableDataReaderRoleId)
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Grants read access to Azure Storage tables for proximity-storage-reader'
  }
}

resource readerBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, readerPrincipalId, storageBlobDataReaderRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataReaderRoleId)
    principalId: readerPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Grants read access to Azure Storage blobs for proximity-storage-reader'
  }
}

// Writer Role Assignments - Write access to tables and blobs
resource writerTableRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, writerPrincipalId, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageTableDataContributorRoleId
    )
    principalId: writerPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Grants read/write access to Azure Storage tables for proximity-storage-writer'
  }
}

resource writerBlobRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, writerPrincipalId, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageBlobDataContributorRoleId
    )
    principalId: writerPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Grants read/write access to Azure Storage blobs for proximity-storage-writer'
  }
}

// Contributor Role Assignment - Full access to storage account
resource contributorRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(storageAccountId, contributorPrincipalId, contributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', contributorRoleId)
    principalId: contributorPrincipalId
    principalType: 'ServicePrincipal'
    description: 'Grants full contributor access to the storage account for proximity-storage-contributor'
  }
}

// Outputs
output readerTableRoleAssignmentId string = readerTableRole.id
output readerBlobRoleAssignmentId string = readerBlobRole.id
output writerTableRoleAssignmentId string = writerTableRole.id
output writerBlobRoleAssignmentId string = writerBlobRole.id
output contributorRoleAssignmentId string = contributorRole.id

// Role Definition IDs for reference
output storageTableDataReaderRoleId string = storageTableDataReaderRoleId
output storageBlobDataReaderRoleId string = storageBlobDataReaderRoleId
output storageTableDataContributorRoleId string = storageTableDataContributorRoleId
output storageBlobDataContributorRoleId string = storageBlobDataContributorRoleId
output contributorRoleId string = contributorRoleId

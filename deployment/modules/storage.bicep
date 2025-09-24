// Storage module - Storage accounts and private endpoints
param location string
param uniqueSuffix string
param functionsSubnetId string
param backendSubnetId string
param blobPrivateDnsZoneId string
param tablePrivateDnsZoneId string
param queuePrivateDnsZoneId string
param filePrivateDnsZoneId string

// Generate storage account names
var storageAccountName = 'proxdata${take(uniqueSuffix, 12)}'
var functionAppStorageAccountName = 'proxfnstorage${take(uniqueSuffix, 10)}'

// Main Storage Account for application data
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false // Disable storage account key access
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Disabled' // Disable internet access
  }
}

// Function App Storage Account
resource functionAppStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: functionAppStorageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false // Disable shared key access for enhanced security with managed identity
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    publicNetworkAccess: 'Disabled' // Disable internet access, will use private endpoints
  }
}

// Private Endpoints for Main Storage Account
module mainStoragePrivateEndpoints 'storage-privateendpoints.bicep' = {
  name: 'main-storage-private-endpoints'
  params: {
    location: location
    storageAccountId: storageAccount.id
    subnetId: backendSubnetId
    namePrefix: 'proxdata'
    blobPrivateDnsZoneId: blobPrivateDnsZoneId
    tablePrivateDnsZoneId: tablePrivateDnsZoneId
    queuePrivateDnsZoneId: queuePrivateDnsZoneId
    filePrivateDnsZoneId: filePrivateDnsZoneId
    blobIpAddress: '10.10.1.133'
    tableIpAddress: '10.10.1.132'
    queueIpAddress: ''
    fileIpAddress: ''
    includeBlob: true
    includeTable: true
    includeQueue: false
    includeFile: false
  }
}

// Private Endpoints for Function App Storage Account
module functionAppStoragePrivateEndpoints 'storage-privateendpoints.bicep' = {
  name: 'functionapp-storage-private-endpoints'
  params: {
    location: location
    storageAccountId: functionAppStorageAccount.id
    subnetId: functionsSubnetId
    namePrefix: 'proxfnstorage'
    blobPrivateDnsZoneId: blobPrivateDnsZoneId
    tablePrivateDnsZoneId: tablePrivateDnsZoneId
    queuePrivateDnsZoneId: queuePrivateDnsZoneId
    filePrivateDnsZoneId: filePrivateDnsZoneId
    blobIpAddress: '10.10.1.14'
    tableIpAddress: '10.10.1.12'
    queueIpAddress: '10.10.1.13'
    fileIpAddress: '10.10.1.15'
    includeBlob: true
    includeTable: true
    includeQueue: true
    includeFile: true
  }
}

// Outputs
output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output functionAppStorageAccountName string = functionAppStorageAccount.name
output functionAppStorageAccountId string = functionAppStorageAccount.id
output storageTablePrivateEndpointId string = mainStoragePrivateEndpoints.outputs.tablePrivateEndpointId
output storageBlobPrivateEndpointId string = mainStoragePrivateEndpoints.outputs.blobPrivateEndpointId
output functionAppStorageTablePrivateEndpointId string = functionAppStoragePrivateEndpoints.outputs.tablePrivateEndpointId
output functionAppStorageQueuePrivateEndpointId string = functionAppStoragePrivateEndpoints.outputs.queuePrivateEndpointId
output functionAppStorageBlobPrivateEndpointId string = functionAppStoragePrivateEndpoints.outputs.blobPrivateEndpointId
output functionAppStorageFilePrivateEndpointId string = functionAppStoragePrivateEndpoints.outputs.filePrivateEndpointId

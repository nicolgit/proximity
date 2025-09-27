// Main template - Proximity application infrastructure
// Parameters
param location string = resourceGroup().location
param vnetName string = 'proximity-net'
param staticWebAppName string = 'ppp-duckiesfarm-com'
param repositoryUrl string = 'https://github.com/CaledosLab/aaa003'
param branch string = 'develop'

// NOTE: For Flex Consumption plans with VNet integration, ensure that the 
// Microsoft.App resource provider is registered in your subscription.
// Run: az provider register --namespace Microsoft.App

// Generate unique suffix for storage account name (must be globally unique)
var uniqueSuffix = uniqueString(resourceGroup().id)

// Deploy networking infrastructure
module networking 'modules/networking.bicep' = {
  name: 'networking-deployment'
  params: {
    location: location
    vnetName: vnetName
  }
}

// Deploy private DNS zones
module privateDns 'modules/privatedns.bicep' = {
  name: 'privatedns-deployment'
  params: {
    vnetId: networking.outputs.vnetId
    vnetName: vnetName
  }
}

// Deploy storage infrastructure
module storage 'modules/storage.bicep' = {
  name: 'storage-deployment'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
    functionsSubnetId: networking.outputs.functionsSubnetId
    backendSubnetId: networking.outputs.backendSubnetId
    blobPrivateDnsZoneId: privateDns.outputs.blobPrivateDnsZoneId
    tablePrivateDnsZoneId: privateDns.outputs.tablePrivateDnsZoneId
    queuePrivateDnsZoneId: privateDns.outputs.queuePrivateDnsZoneId
    filePrivateDnsZoneId: privateDns.outputs.filePrivateDnsZoneId
  }
}

// Deploy Function App
module functionApp 'modules/functionapp.bicep' = {
  name: 'functionapp-deployment'
  params: {
    location: location
    uniqueSuffix: uniqueSuffix
    integrationSubnetId: networking.outputs.integrationSubnetId
    functionAppStorageAccountName: storage.outputs.functionAppStorageAccountName
    functionAppStorageAccountId: storage.outputs.functionAppStorageAccountId
    mainStorageAccountName: storage.outputs.storageAccountName
    mainStorageAccountId: storage.outputs.storageAccountId
  }
}

// Deploy Static Web App
module staticWebApp 'modules/staticwebapp.bicep' = {
  name: 'staticwebapp-deployment'
  params: {
    location: location
  }
}





// Outputs
output vnetId string = networking.outputs.vnetId
output vnetName string = networking.outputs.vnetName
output functionsSubnetId string = networking.outputs.functionsSubnetId
output integrationSubnetId string = networking.outputs.integrationSubnetId
output backendSubnetId string = networking.outputs.backendSubnetId
output storageAccountName string = storage.outputs.storageAccountName
output storageAccountId string = storage.outputs.storageAccountId
output storageTablePrivateEndpointId string = storage.outputs.storageTablePrivateEndpointId
output storageBlobPrivateEndpointId string = storage.outputs.storageBlobPrivateEndpointId
output staticWebAppId string = staticWebApp.outputs.staticWebAppId
output staticWebAppName string = staticWebApp.outputs.staticWebAppName
output staticWebAppUrl string = staticWebApp.outputs.staticWebAppUrl
output functionAppId string = functionApp.outputs.functionAppId
output functionAppName string = functionApp.outputs.functionAppName
output functionAppServicePlanId string = functionApp.outputs.functionAppServicePlanId
output functionAppServicePlanName string = functionApp.outputs.functionAppServicePlanName
output functionAppStorageAccountName string = storage.outputs.functionAppStorageAccountName
output functionAppStorageAccountId string = storage.outputs.functionAppStorageAccountId
output functionAppStorageTablePrivateEndpointId string = storage.outputs.functionAppStorageTablePrivateEndpointId
output functionAppStorageQueuePrivateEndpointId string = storage.outputs.functionAppStorageQueuePrivateEndpointId
output functionAppStorageBlobPrivateEndpointId string = storage.outputs.functionAppStorageBlobPrivateEndpointId
output functionAppStorageFilePrivateEndpointId string = storage.outputs.functionAppStorageFilePrivateEndpointId

// Private DNS Zone Outputs
output blobPrivateDnsZoneId string = privateDns.outputs.blobPrivateDnsZoneId
output blobPrivateDnsZoneName string = privateDns.outputs.blobPrivateDnsZoneName
output tablePrivateDnsZoneId string = privateDns.outputs.tablePrivateDnsZoneId
output tablePrivateDnsZoneName string = privateDns.outputs.tablePrivateDnsZoneName
output queuePrivateDnsZoneId string = privateDns.outputs.queuePrivateDnsZoneId
output queuePrivateDnsZoneName string = privateDns.outputs.queuePrivateDnsZoneName
output filePrivateDnsZoneId string = privateDns.outputs.filePrivateDnsZoneId
output filePrivateDnsZoneName string = privateDns.outputs.filePrivateDnsZoneName





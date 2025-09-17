// Parameters
param location string = resourceGroup().location
param vnetName string = 'proximity-net'
param staticWebAppName string = 'ppp-duckiesfarm-com'
param repositoryUrl string = 'https://github.com/nicolgit/proximity'
param branch string = 'develop'

// Variables
var vnetConfig = {
  addressSpace: '10.10.1.0/24'
  subnets: {
    functions: {
      name: 'functions-subnet'
      addressPrefix: '10.10.1.0/26'
    }
    integration: {
      name: 'integration-subnet'
      addressPrefix: '10.10.1.64/26'
    }
    backend: {
      name: 'backend-subnet'
      addressPrefix: '10.10.1.128/26'
    }
  }
}

// Generate unique suffix for storage account name (must be globally unique)
var uniqueSuffix = uniqueString(resourceGroup().id)
var storageAccountName = 'proxdata${take(uniqueSuffix, 12)}'

// Virtual Network
resource vnet 'Microsoft.Network/virtualNetworks@2023-09-01' = {
  name: vnetName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        vnetConfig.addressSpace
      ]
    }
    subnets: [
      {
        name: vnetConfig.subnets.functions.name
        properties: {
          addressPrefix: vnetConfig.subnets.functions.addressPrefix
        }
      }
      {
        name: vnetConfig.subnets.integration.name
        properties: {
          addressPrefix: vnetConfig.subnets.integration.addressPrefix
        }
      }
      {
        name: vnetConfig.subnets.backend.name
        properties: {
          addressPrefix: vnetConfig.subnets.backend.addressPrefix
        }
      }
    ]
  }
}

// Storage Account
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

// Private Endpoint for Storage Table
resource storageTablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: 'proxdata-table-pep'
  location: location
  properties: {
    subnet: {
      id: '${vnet.id}/subnets/${vnetConfig.subnets.backend.name}'
    }
    customNetworkInterfaceName: 'proxdata-table-pep-nic'
    ipConfigurations: [
      {
        name: 'table-ip-config'
        properties: {
          privateIPAddress: '10.10.1.133'
          groupId: 'table'
          memberName: 'table'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: 'proxdata-table-connection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
}

// Private Endpoint for Storage Blob
resource storageBlobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: 'proxdata-blob-pep'
  location: location
  properties: {
    subnet: {
      id: '${vnet.id}/subnets/${vnetConfig.subnets.backend.name}'
    }
    customNetworkInterfaceName: 'proxdata-blob-pep-nic'
    ipConfigurations: [
      {
        name: 'blob-ip-config'
        properties: {
          privateIPAddress: '10.10.1.132'
          groupId: 'blob'
          memberName: 'blob'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: 'proxdata-blob-connection'
        properties: {
          privateLinkServiceId: storageAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

// Static Web App
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: staticWebAppName
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    repositoryUrl: repositoryUrl
    branch: branch
    buildProperties: {
      appLocation: '/spa'
      outputLocation: 'dist'
    }
  }
}

// Outputs
output vnetId string = vnet.id
output vnetName string = vnet.name
output functionsSubnetId string = '${vnet.id}/subnets/${vnetConfig.subnets.functions.name}'
output integrationSubnetId string = '${vnet.id}/subnets/${vnetConfig.subnets.integration.name}'
output backendSubnetId string = '${vnet.id}/subnets/${vnetConfig.subnets.backend.name}'
output storageAccountName string = storageAccount.name
output storageAccountId string = storageAccount.id
output storageTablePrivateEndpointId string = storageTablePrivateEndpoint.id
output storageBlobPrivateEndpointId string = storageBlobPrivateEndpoint.id
output staticWebAppId string = staticWebApp.id
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = staticWebApp.properties.defaultHostname

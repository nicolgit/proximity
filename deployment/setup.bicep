// Parameters
param location string = resourceGroup().location
param vnetName string = 'proximity-net'
param staticWebAppName string = 'ppp-duckiesfarm-com'
param repositoryUrl string = 'https://github.com/nicolgit/proximity'
param branch string = 'develop'

// NOTE: For Flex Consumption plans with VNet integration, ensure that the 
// Microsoft.App resource provider is registered in your subscription.
// Run: az provider register --namespace Microsoft.App

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
var functionAppStorageAccountName = 'proxfnstorage${take(uniqueSuffix, 10)}'
var functionAppName = 'api-backend-${take(uniqueSuffix, 6)}'

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
          // Required delegation for Azure Functions Flex Consumption plan VNet integration
          delegations: [
            {
              name: 'Microsoft.App.environments'
              properties: {
                serviceName: 'Microsoft.App/environments'
              }
            }
          ]
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

// Private DNS Zones for Storage Services
resource blobPrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.blob.${environment().suffixes.storage}'
  location: 'global'
}

resource tablePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.table.${environment().suffixes.storage}'
  location: 'global'
}

resource queuePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.queue.${environment().suffixes.storage}'
  location: 'global'
}

resource filePrivateDnsZone 'Microsoft.Network/privateDnsZones@2020-06-01' = {
  name: 'privatelink.file.${environment().suffixes.storage}'
  location: 'global'
}

// Virtual Network Links for Private DNS Zones
resource blobPrivateDnsZoneVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: blobPrivateDnsZone
  name: '${vnetName}-blob-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

resource tablePrivateDnsZoneVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: tablePrivateDnsZone
  name: '${vnetName}-table-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

resource queuePrivateDnsZoneVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: queuePrivateDnsZone
  name: '${vnetName}-queue-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
  }
}

resource filePrivateDnsZoneVnetLink 'Microsoft.Network/privateDnsZones/virtualNetworkLinks@2020-06-01' = {
  parent: filePrivateDnsZone
  name: '${vnetName}-file-link'
  location: 'global'
  properties: {
    registrationEnabled: false
    virtualNetwork: {
      id: vnet.id
    }
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
          privateIPAddress: '10.10.1.132'
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

resource storageTablePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: storageTablePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'table-config'
        properties: {
          privateDnsZoneId: tablePrivateDnsZone.id
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
          privateIPAddress: '10.10.1.133'
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

resource storageBlobPrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: storageBlobPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob-config'
        properties: {
          privateDnsZoneId: blobPrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Function App Storage Table
resource functionAppStorageTablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: 'proxfnstorage-table-pep'
  location: location
  properties: {
    subnet: {
      id: '${vnet.id}/subnets/${vnetConfig.subnets.functions.name}'
    }
    customNetworkInterfaceName: 'proxfnstorage-table-pep-nic'
    ipConfigurations: [
      {
        name: 'table-ip-config'
        properties: {
          privateIPAddress: '10.10.1.12'
          groupId: 'table'
          memberName: 'table'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: 'proxfnstorage-table-connection'
        properties: {
          privateLinkServiceId: functionAppStorageAccount.id
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
}

resource functionAppStorageTablePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: functionAppStorageTablePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'table-config'
        properties: {
          privateDnsZoneId: tablePrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Function App Storage Queue
resource functionAppStorageQueuePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: 'proxfnstorage-queue-pep'
  location: location
  properties: {
    subnet: {
      id: '${vnet.id}/subnets/${vnetConfig.subnets.functions.name}'
    }
    customNetworkInterfaceName: 'proxfnstorage-queue-pep-nic'
    ipConfigurations: [
      {
        name: 'queue-ip-config'
        properties: {
          privateIPAddress: '10.10.1.13'
          groupId: 'queue'
          memberName: 'queue'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: 'proxfnstorage-queue-connection'
        properties: {
          privateLinkServiceId: functionAppStorageAccount.id
          groupIds: [
            'queue'
          ]
        }
      }
    ]
  }
}

resource functionAppStorageQueuePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: functionAppStorageQueuePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'queue-config'
        properties: {
          privateDnsZoneId: queuePrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Function App Storage Blob
resource functionAppStorageBlobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: 'proxfnstorage-blob-pep'
  location: location
  properties: {
    subnet: {
      id: '${vnet.id}/subnets/${vnetConfig.subnets.functions.name}'
    }
    customNetworkInterfaceName: 'proxfnstorage-blob-pep-nic'
    ipConfigurations: [
      {
        name: 'blob-ip-config'
        properties: {
          privateIPAddress: '10.10.1.14'
          groupId: 'blob'
          memberName: 'blob'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: 'proxfnstorage-blob-connection'
        properties: {
          privateLinkServiceId: functionAppStorageAccount.id
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource functionAppStorageBlobPrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: functionAppStorageBlobPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob-config'
        properties: {
          privateDnsZoneId: blobPrivateDnsZone.id
        }
      }
    ]
  }
}

// Private Endpoint for Function App Storage File
resource functionAppStorageFilePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = {
  name: 'proxfnstorage-file-pep'
  location: location
  properties: {
    subnet: {
      id: '${vnet.id}/subnets/${vnetConfig.subnets.functions.name}'
    }
    customNetworkInterfaceName: 'proxfnstorage-file-pep-nic'
    ipConfigurations: [
      {
        name: 'file-ip-config'
        properties: {
          privateIPAddress: '10.10.1.15'
          groupId: 'file'
          memberName: 'file'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: 'proxfnstorage-file-connection'
        properties: {
          privateLinkServiceId: functionAppStorageAccount.id
          groupIds: [
            'file'
          ]
        }
      }
    ]
  }
}

resource functionAppStorageFilePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = {
  parent: functionAppStorageFilePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'file-config'
        properties: {
          privateDnsZoneId: filePrivateDnsZone.id
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

// Function App Service Plan (Flex Consumption)
resource functionAppServicePlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'flex-consumption-plan'
  location: location
  sku: {
    name: 'FC1'
    tier: 'FlexConsumption'
  }
  properties: {
    reserved: true // Required for Linux
  }
}

// Function App (Flex Consumption)
resource functionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp,linux'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: functionAppServicePlan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: '${functionAppStorageAccount.properties.primaryEndpoints.blob}deployments'
          authentication: {
            type: 'SystemAssignedIdentity'
          }
        }
      }
      scaleAndConcurrency: {
        maximumInstanceCount: 1000
        instanceMemoryMB: 512
      }
      runtime: {
        name: 'dotnet-isolated'
        version: '8.0'
      }
    }
    vnetContentShareEnabled: true
    vnetImagePullEnabled: true
    vnetRouteAllEnabled: true
    // VNet integration for Flex Consumption - configure via virtualNetworkSubnetId
    virtualNetworkSubnetId: '${vnet.id}/subnets/${vnetConfig.subnets.integration.name}'
    siteConfig: {
      cors: {
        allowedOrigins: ['*']
      }
      use32BitWorkerProcess: false
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage__accountName'
          value: functionAppStorageAccount.name
        }
        {
          name: 'AzureWebJobsStorage__credential'
          value: 'managedidentity'
        }
        {
          name: 'FUNCTIONS_EXTENSION_VERSION'
          value: '~4'
        }
        {
          name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING__accountName'
          value: functionAppStorageAccount.name
        }
        {
          name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING__credential'
          value: 'managedidentity'
        }
      ]
    }
  }
}

// Role assignment: Storage Blob Data Owner for Function App managed identity
resource functionAppStorageBlobDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionAppStorageAccount.id, functionApp.id, 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
  scope: functionAppStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b') // Storage Blob Data Owner
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Storage Account Contributor for Function App managed identity
resource functionAppStorageAccountContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionAppStorageAccount.id, functionApp.id, '17d1049b-9a84-46fb-8f53-869881c3d3ab')
  scope: functionAppStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '17d1049b-9a84-46fb-8f53-869881c3d3ab') // Storage Account Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
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
output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppServicePlanId string = functionAppServicePlan.id
output functionAppServicePlanName string = functionAppServicePlan.name
output functionAppStorageAccountName string = functionAppStorageAccount.name
output functionAppStorageAccountId string = functionAppStorageAccount.id
output functionAppStorageTablePrivateEndpointId string = functionAppStorageTablePrivateEndpoint.id
output functionAppStorageQueuePrivateEndpointId string = functionAppStorageQueuePrivateEndpoint.id
output functionAppStorageBlobPrivateEndpointId string = functionAppStorageBlobPrivateEndpoint.id
output functionAppStorageFilePrivateEndpointId string = functionAppStorageFilePrivateEndpoint.id

// Private DNS Zone Outputs
output blobPrivateDnsZoneId string = blobPrivateDnsZone.id
output blobPrivateDnsZoneName string = blobPrivateDnsZone.name
output tablePrivateDnsZoneId string = tablePrivateDnsZone.id
output tablePrivateDnsZoneName string = tablePrivateDnsZone.name
output queuePrivateDnsZoneId string = queuePrivateDnsZone.id
output queuePrivateDnsZoneName string = queuePrivateDnsZone.name
output filePrivateDnsZoneId string = filePrivateDnsZone.id
output filePrivateDnsZoneName string = filePrivateDnsZone.name

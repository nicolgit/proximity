// Function App module - Azure Functions with Flex Consumption plan
param location string
param uniqueSuffix string
param integrationSubnetId string
param functionAppStorageAccountName string
param functionAppStorageAccountId string
param mainStorageAccountName string
param mainStorageAccountId string
param applicationInsightsConnectionString string
param applicationInsightsInstrumentationKey string
param logAnalyticsWorkspaceId string

// Generate function app name
var functionAppName = 'api-backend-${take(uniqueSuffix, 6)}'

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
          value: '${functionAppStorageAccount.properties.primaryEndpoints.blob}${deploymentsContainer.name}'
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
    virtualNetworkSubnetId: integrationSubnetId
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
          value: functionAppStorageAccountName
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
          value: functionAppStorageAccountName
        }
        {
          name: 'DEPLOYMENT_STORAGE_CONNECTION_STRING__credential'
          value: 'managedidentity'
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: applicationInsightsConnectionString
        }
        {
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: applicationInsightsInstrumentationKey
        }
        {
          name: 'ApplicationInsightsAgent_EXTENSION_VERSION'
          value: '~3'
        }
        {
          name: 'authentication'
          value: 'identity'
        }
        {
          name: 'blobUri'
          value: 'https://${mainStorageAccountName}.blob.${environment().suffixes.storage}/'
        }
        {
          name: 'tableUri'
          value: 'https://${mainStorageAccountName}.table.${environment().suffixes.storage}/'
        }
      ]
    }
  }
}

// Get reference to the function app storage account for role assignments
resource functionAppStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: functionAppStorageAccountName
}

// Create blob services for the function app storage account
resource functionAppBlobServices 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = {
  name: 'default'
  parent: functionAppStorageAccount
}

// Create deployments container in function app storage account
resource deploymentsContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = {
  name: 'deployments'
  parent: functionAppBlobServices
  properties: {
    publicAccess: 'None'
  }
}

// Role assignment: Storage Blob Data Owner for Function App managed identity
resource functionAppStorageBlobDataOwnerRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionAppStorageAccountId, functionApp.id, 'b7e6dc6d-f1e8-4753-8033-0f276bb0955b')
  scope: functionAppStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'b7e6dc6d-f1e8-4753-8033-0f276bb0955b'
    ) // Storage Blob Data Owner
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Storage Account Contributor for Function App managed identity
resource functionAppStorageAccountContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(functionAppStorageAccountId, functionApp.id, '17d1049b-9a84-46fb-8f53-869881c3d3ab')
  scope: functionAppStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '17d1049b-9a84-46fb-8f53-869881c3d3ab'
    ) // Storage Account Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Get reference to the main storage account for role assignments
resource mainStorageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' existing = {
  name: mainStorageAccountName
}

// Role assignment: Storage Blob Data Contributor for Function App managed identity on main storage account
resource mainStorageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(mainStorageAccountId, functionApp.id, 'ba92f5b4-2d11-453d-a403-e96b0029c9fe')
  scope: mainStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'ba92f5b4-2d11-453d-a403-e96b0029c9fe'
    ) // Storage Blob Data Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Role assignment: Storage Table Data Contributor for Function App managed identity on main storage account
resource mainStorageTableDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(mainStorageAccountId, functionApp.id, '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3')
  scope: mainStorageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'
    ) // Storage Table Data Contributor
    principalId: functionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// Diagnostic Settings for Function App to send logs to Log Analytics
resource functionAppDiagnosticSettings 'Microsoft.Insights/diagnosticSettings@2021-05-01-preview' = {
  name: 'functionapp-diagnostics'
  scope: functionApp
  properties: {
    workspaceId: logAnalyticsWorkspaceId
    logs: [
      {
        categoryGroup: 'allLogs'
        enabled: true
        retentionPolicy: {
          enabled: false
          days: 0
        }
      }
    ]
  }
}

// Outputs
output functionAppId string = functionApp.id
output functionAppName string = functionApp.name
output functionAppServicePlanId string = functionAppServicePlan.id
output functionAppServicePlanName string = functionAppServicePlan.name
output functionAppDiagnosticSettingsId string = functionAppDiagnosticSettings.id

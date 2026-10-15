targetScope = 'resourceGroup'

@description('Name of the Azure SRE Agent.')
param agentName string

@description('Azure region for the SRE Agent and all supporting resources.')
param location string

@description('Existing resource group monitored by the SRE Agent.')
param monitoredResourceGroupName string

@description('Full resource ID of the existing subnet delegated to Microsoft.App/environments.')
param subnetResourceId string

var suffix = uniqueString(subscription().subscriptionId, resourceGroup().id, agentName)
var monitoredResourceGroupId = subscriptionResourceId('Microsoft.Resources/resourceGroups', monitoredResourceGroupName)

resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-${suffix}'
  location: location
  properties: {
    retentionInDays: 30
    sku: {
      name: 'PerGB2018'
    }
  }
}

resource applicationInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-${suffix}'
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'SreAgent'
    WorkspaceResourceId: logAnalyticsWorkspace.id
  }
}

#disable-next-line BCP073
resource managedIdentity 'Microsoft.ManagedIdentity/userAssignedIdentities@2024-11-30' = {
  name: '${agentName}-identity'
  location: location
  properties: {
    isolationScope: 'Regional'
  }
}

module managedIdentityRbac './target-rbac.bicep' = {
  name: 'managed-identity-rbac'
  scope: resourceGroup(subscription().subscriptionId, monitoredResourceGroupName)
  params: {
    principalId: managedIdentity.properties.principalId
  }
}

#disable-next-line BCP081
resource sreAgent 'Microsoft.App/agents@2025-05-01-preview' = {
  name: agentName
  location: location
  identity: {
    type: 'SystemAssigned, UserAssigned'
    userAssignedIdentities: {
      '${managedIdentity.id}': {}
    }
  }
  properties: {
    knowledgeGraphConfiguration: {
      identity: managedIdentity.id
      managedResources: [
        monitoredResourceGroupId
      ]
    }
    actionConfiguration: {
      accessLevel: 'Low'
      identity: managedIdentity.id
      mode: 'Review'
    }
    logConfiguration: {
      applicationInsightsConfiguration: {
        appId: applicationInsights.properties.AppId
        connectionString: applicationInsights.properties.ConnectionString
        applicationInsightsResourceId: applicationInsights.id
      }
    }
    upgradeChannel: 'Stable'
    monthlyAgentUnitLimit: 10000
    defaultModel: {
      provider: 'MicrosoftFoundry'
      name: 'Automatic'
    }
    experimentalSettings: {
      EnableWorkspaceTools: true
      EnableHttpTriggers: true
      EnableV2AgentLoop: true
    }
    vnetConfiguration: {
      subnetResourceId: subnetResourceId
    }
    sandboxConfiguration: {
      egress: {
        mode: 'AzureVNet'
        allowedHosts: []
        allowedRegistries: []
        allowedCodeRepositories: [
          'Github'
        ]
        allowHttpMcpServerNetworkAccess: false
        vnetConfiguration: {
          usePrivateDnsResolution: false
        }
      }
    }
  }
  dependsOn: [
    managedIdentityRbac
  ]
}

module systemIdentityRbac './target-rbac.bicep' = {
  name: 'system-identity-rbac'
  scope: resourceGroup(subscription().subscriptionId, monitoredResourceGroupName)
  params: {
    principalId: sreAgent.identity.principalId
  }
}

resource deployerAdminRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(sreAgent.id, deployer().objectId, 'e79298df-d852-4c6d-84f9-5d13249d1e55')
  scope: sreAgent
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      'e79298df-d852-4c6d-84f9-5d13249d1e55'
    )
    principalId: deployer().objectId
    principalType: 'User'
  }
}

output agentId string = sreAgent.id

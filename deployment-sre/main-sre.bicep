targetScope = 'subscription'

@description('Name of the resource group that contains the Azure SRE Agent and its supporting resources.')
param agentResourceGroupName string = 'proximity-sre'

@description('Name of the Azure SRE Agent.')
param agentName string = 'proximity-sre-agent'

@description('Azure region for the SRE Agent and all supporting resources.')
param location string = 'italynorth'

@description('Existing resource group monitored by the SRE Agent.')
param monitoredResourceGroupName string = 'proximity-playground'

@description('Resource group that contains the existing virtual network.')
param networkResourceGroupName string = 'proximity-playground'

@description('Name of the existing virtual network used by the SRE Agent.')
param virtualNetworkName string = 'proximity-net'

@description('Name of the existing subnet delegated to Microsoft.App/environments.')
param subnetName string = 'sre-subnet'

@description('GitHub repository to add to the SRE Agent Knowledge Base after deployment.')
param knowledgeBaseRepositoryUrl string = 'https://github.com/nicolgit/proximity'

var subnetResourceId = resourceId(
  subscription().subscriptionId,
  networkResourceGroupName,
  'Microsoft.Network/virtualNetworks/subnets',
  virtualNetworkName,
  subnetName
)

resource monitoredResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' existing = {
  name: monitoredResourceGroupName
}

resource networkResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' existing = {
  name: networkResourceGroupName
}

resource agentResourceGroup 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: agentResourceGroupName
  location: location
}

module agent './sre-agent.bicep' = {
  name: 'deploy-${agentName}'
  scope: agentResourceGroup
  params: {
    agentName: agentName
    location: location
    monitoredResourceGroupName: monitoredResourceGroup.name
    subnetResourceId: subnetResourceId
  }
  dependsOn: [
    networkResourceGroup
  ]
}

output agentId string = agent.outputs.agentId
output agentPortalUrl string = 'https://sre.azure.com/#/agent/${subscription().subscriptionId}/${agentResourceGroupName}/${agentName}'
output agentResourceGroupId string = agentResourceGroup.id
output knowledgeBaseRepositoryUrl string = knowledgeBaseRepositoryUrl
output knowledgeBaseConfigurationRequired string = 'Add the repository shown in knowledgeBaseRepositoryUrl from Builder > Knowledge base after deployment.'

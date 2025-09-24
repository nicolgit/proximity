// Networking module - VNet and subnets configuration
param location string
param vnetName string

// Variables for network configuration
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

// Virtual Network with subnets
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

// Outputs
output vnetId string = vnet.id
output vnetName string = vnet.name
output functionsSubnetId string = '${vnet.id}/subnets/${vnetConfig.subnets.functions.name}'
output integrationSubnetId string = '${vnet.id}/subnets/${vnetConfig.subnets.integration.name}'
output backendSubnetId string = '${vnet.id}/subnets/${vnetConfig.subnets.backend.name}'

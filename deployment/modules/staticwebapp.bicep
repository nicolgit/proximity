// Static Web App module - Azure Static Web Apps configuration
param location string

// Static Web App
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'swa-${uniqueString(resourceGroup().id)}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    // Repository configuration will be set up manually after deployment
  }
}

// Outputs
output staticWebAppId string = staticWebApp.id
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = staticWebApp.properties.defaultHostname

// Static Web App module - Azure Static Web Apps configuration
param location string
param functionAppId string
param uniqueSuffix string

// Static Web App
resource staticWebApp 'Microsoft.Web/staticSites@2023-01-01' = {
  name: 'proximity-spa-${uniqueSuffix}'
  location: location
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    // Repository configuration will be set up manually after deployment
    enterpriseGradeCdnStatus: 'Enabled' // Enable CDN for both static content and API routes
    publicNetworkAccess: 'Enabled'
  }
}

// Link the Function App as the API backend for the Static Web App
resource staticWebAppApiBackend 'Microsoft.Web/staticSites/linkedBackends@2023-01-01' = {
  name: 'production'
  parent: staticWebApp
  properties: {
    backendResourceId: functionAppId
    region: location
  }
}

// Outputs
output staticWebAppId string = staticWebApp.id
output staticWebAppName string = staticWebApp.name
output staticWebAppUrl string = staticWebApp.properties.defaultHostname
output staticWebAppApiBackendId string = staticWebAppApiBackend.id

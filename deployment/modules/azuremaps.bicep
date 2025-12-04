// Azure Maps module - Location services for proximity application
param location string
param uniqueSuffix string
param mapsAccountName string = 'proximity-maps-${take(uniqueSuffix, 6)}'

// Azure Maps Account
resource mapsAccount 'Microsoft.Maps/accounts@2024-07-01-preview' = {
  name: mapsAccountName
  location: location
  sku: {
    name: 'G2'
  }
  kind: 'Gen2'
  properties: {
    disableLocalAuth: false
    cors: {
      corsRules: [
        {
          allowedOrigins: ['https://proximity.duckiesfarm.com']
        }
      ]
    }
  }
  tags: {
    environment: 'proximity-app'
  }
}

// Outputs
output mapsAccountId string = mapsAccount.id
output mapsAccountName string = mapsAccount.name
@secure()
output mapsAccountPrimaryKey string = mapsAccount.listKeys().primaryKey
output mapsAccountClientId string = mapsAccount.properties.uniqueId

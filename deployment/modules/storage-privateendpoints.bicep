// Reusable storage private endpoints module
param location string
param storageAccountId string
param subnetId string
param namePrefix string
param blobPrivateDnsZoneId string
param tablePrivateDnsZoneId string
param queuePrivateDnsZoneId string = ''
param filePrivateDnsZoneId string = ''
param blobIpAddress string
param tableIpAddress string
param queueIpAddress string = ''
param fileIpAddress string = ''
param includeBlob bool = true
param includeTable bool = true
param includeQueue bool = false
param includeFile bool = false

// Blob Private Endpoint
resource blobPrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (includeBlob) {
  name: '${namePrefix}-blob-pep'
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    customNetworkInterfaceName: '${namePrefix}-blob-pep-nic'
    ipConfigurations: [
      {
        name: 'blob-ip-config'
        properties: {
          privateIPAddress: blobIpAddress
          groupId: 'blob'
          memberName: 'blob'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-blob-connection'
        properties: {
          privateLinkServiceId: storageAccountId
          groupIds: [
            'blob'
          ]
        }
      }
    ]
  }
}

resource blobPrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (includeBlob) {
  parent: blobPrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'blob-config'
        properties: {
          privateDnsZoneId: blobPrivateDnsZoneId
        }
      }
    ]
  }
}

// Table Private Endpoint
resource tablePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (includeTable) {
  name: '${namePrefix}-table-pep'
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    customNetworkInterfaceName: '${namePrefix}-table-pep-nic'
    ipConfigurations: [
      {
        name: 'table-ip-config'
        properties: {
          privateIPAddress: tableIpAddress
          groupId: 'table'
          memberName: 'table'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-table-connection'
        properties: {
          privateLinkServiceId: storageAccountId
          groupIds: [
            'table'
          ]
        }
      }
    ]
  }
}

resource tablePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (includeTable) {
  parent: tablePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'table-config'
        properties: {
          privateDnsZoneId: tablePrivateDnsZoneId
        }
      }
    ]
  }
}

// Queue Private Endpoint
resource queuePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (includeQueue && !empty(queuePrivateDnsZoneId)) {
  name: '${namePrefix}-queue-pep'
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    customNetworkInterfaceName: '${namePrefix}-queue-pep-nic'
    ipConfigurations: [
      {
        name: 'queue-ip-config'
        properties: {
          privateIPAddress: queueIpAddress
          groupId: 'queue'
          memberName: 'queue'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-queue-connection'
        properties: {
          privateLinkServiceId: storageAccountId
          groupIds: [
            'queue'
          ]
        }
      }
    ]
  }
}

resource queuePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (includeQueue && !empty(queuePrivateDnsZoneId)) {
  parent: queuePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'queue-config'
        properties: {
          privateDnsZoneId: queuePrivateDnsZoneId
        }
      }
    ]
  }
}

// File Private Endpoint
resource filePrivateEndpoint 'Microsoft.Network/privateEndpoints@2023-09-01' = if (includeFile && !empty(filePrivateDnsZoneId)) {
  name: '${namePrefix}-file-pep'
  location: location
  properties: {
    subnet: {
      id: subnetId
    }
    customNetworkInterfaceName: '${namePrefix}-file-pep-nic'
    ipConfigurations: [
      {
        name: 'file-ip-config'
        properties: {
          privateIPAddress: fileIpAddress
          groupId: 'file'
          memberName: 'file'
        }
      }
    ]
    privateLinkServiceConnections: [
      {
        name: '${namePrefix}-file-connection'
        properties: {
          privateLinkServiceId: storageAccountId
          groupIds: [
            'file'
          ]
        }
      }
    ]
  }
}

resource filePrivateEndpointDnsZoneGroup 'Microsoft.Network/privateEndpoints/privateDnsZoneGroups@2023-09-01' = if (includeFile && !empty(filePrivateDnsZoneId)) {
  parent: filePrivateEndpoint
  name: 'default'
  properties: {
    privateDnsZoneConfigs: [
      {
        name: 'file-config'
        properties: {
          privateDnsZoneId: filePrivateDnsZoneId
        }
      }
    ]
  }
}

// Outputs
output blobPrivateEndpointId string = includeBlob ? blobPrivateEndpoint.id : ''
output tablePrivateEndpointId string = includeTable ? tablePrivateEndpoint.id : ''
output queuePrivateEndpointId string = (includeQueue && !empty(queuePrivateDnsZoneId)) ? queuePrivateEndpoint.id : ''
output filePrivateEndpointId string = (includeFile && !empty(filePrivateDnsZoneId)) ? filePrivateEndpoint.id : ''

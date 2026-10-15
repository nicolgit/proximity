extension microsoftGraphV1_0

resource application 'Microsoft.Graph/applications@v1.0' = {
  uniqueName: 'proximity-generator-${uniqueString(resourceGroup().id)}'
  displayName: 'proximity-generator-app'
  signInAudience: 'AzureADMyOrg'
}

resource servicePrincipal 'Microsoft.Graph/servicePrincipals@v1.0' = {
  appId: application.appId
}

output clientId string = application.appId
output applicationObjectId string = application.id
output principalId string = servicePrincipal.id

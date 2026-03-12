targetScope = 'resourceGroup'

@description('Azure region for all resources. Defaults to the resource group location.')
param location string = resourceGroup().location

@description('Environment name (e.g. dev, staging, prod).')
@allowed(['dev', 'staging', 'prod'])
param environmentName string = 'dev'

@description('Publisher email for APIM notifications.')
param apimPublisherEmail string

@description('Publisher display name for APIM.')
param apimPublisherName string = 'Payment Gateway Team'

@description('Principal IDs to assign Key Vault Secrets Officer role.')
param keyVaultSecretsOfficerPrincipalIds array = []

var tags = {
  environment: environmentName
  project: 'payment-gateway'
  managedBy: 'bicep'
}

// ---------------------------------------------------------------------------
// Modules
// ---------------------------------------------------------------------------
module serviceBus 'modules/servicebus.bicep' = {
  name: 'serviceBus'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
  }
}

module cosmosDb 'modules/cosmosdb.bicep' = {
  name: 'cosmosDb'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
  }
}

module keyVault 'modules/keyvault.bicep' = {
  name: 'keyVault'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
    secretsOfficerPrincipalIds: keyVaultSecretsOfficerPrincipalIds
  }
}

module observability 'modules/observability.bicep' = {
  name: 'observability'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
  }
}

module apim 'modules/apim.bicep' = {
  name: 'apim'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
    publisherEmail: apimPublisherEmail
    publisherName: apimPublisherName
    appInsightsId: observability.outputs.appInsightsId
    appInsightsInstrumentationKey: observability.outputs.instrumentationKey
    ingressFunctionAppHostname: functions.outputs.ingressFunctionAppHostname
  }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    location: location
    environmentName: environmentName
    tags: tags
    appInsightsConnectionString: observability.outputs.connectionString
    appInsightsInstrumentationKey: observability.outputs.instrumentationKey
    serviceBusFullyQualifiedNamespace: serviceBus.outputs.fullyQualifiedNamespace
    cosmosDbEndpoint: cosmosDb.outputs.endpoint
    keyVaultUri: keyVault.outputs.keyVaultUri
    serviceBusNamespaceId: serviceBus.outputs.namespaceId
    keyVaultId: keyVault.outputs.keyVaultId
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
output apimGatewayUrl string = apim.outputs.gatewayUrl
output ingressFunctionAppName string = functions.outputs.ingressFunctionAppName
output processorFunctionAppName string = functions.outputs.processorFunctionAppName
output keyVaultUri string = keyVault.outputs.keyVaultUri
output cosmosDbEndpoint string = cosmosDb.outputs.endpoint
output serviceBusNamespace string = serviceBus.outputs.namespaceName

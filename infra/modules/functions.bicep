@description('Azure region for all resources.')
param location string

@description('Environment name used as a suffix.')
param environmentName string

@description('Resource tags.')
param tags object = {}

@description('Application Insights connection string.')
param appInsightsConnectionString string

@description('Application Insights instrumentation key.')
param appInsightsInstrumentationKey string

@description('Service Bus fully-qualified namespace (for passwordless auth).')
param serviceBusFullyQualifiedNamespace string

@description('Cosmos DB endpoint.')
param cosmosDbEndpoint string

@description('Key Vault URI.')
param keyVaultUri string

@description('Service Bus namespace resource ID (for RBAC scope).')
param serviceBusNamespaceId string

@description('Key Vault resource ID (for RBAC scope).')
param keyVaultId string

// ---------------------------------------------------------------------------
// Storage Account (required by Azure Functions runtime)
// ---------------------------------------------------------------------------
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: 'stpaymentgw${environmentName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard_LRS'
  }
  kind: 'StorageV2'
  properties: {
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    allowBlobPublicAccess: false
  }
}

// ---------------------------------------------------------------------------
// Consumption App Service Plan
// ---------------------------------------------------------------------------
resource hostingPlan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: 'asp-payment-gateway-${environmentName}'
  location: location
  tags: tags
  sku: {
    name: 'Y1'
    tier: 'Dynamic'
  }
  properties: {
    reserved: false
  }
}

// Common app settings shared by both function apps
var commonAppSettings = [
  {
    name: 'AzureWebJobsStorage__accountName'
    value: storageAccount.name
  }
  {
    name: 'FUNCTIONS_EXTENSION_VERSION'
    value: '~4'
  }
  {
    name: 'FUNCTIONS_WORKER_RUNTIME'
    value: 'dotnet-isolated'
  }
  {
    name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
    value: appInsightsConnectionString
  }
  {
    name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
    value: appInsightsInstrumentationKey
  }
  {
    name: 'CosmosDb__Endpoint'
    value: cosmosDbEndpoint
  }
  {
    name: 'KeyVaultUri'
    value: keyVaultUri
  }
]

// ---------------------------------------------------------------------------
// Ingress Function App
// ---------------------------------------------------------------------------
resource ingressFunctionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-ingress-${environmentName}'
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: concat(commonAppSettings, [
        {
          name: 'ServiceBusConnection__fullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
      ])
    }
  }
}

// ---------------------------------------------------------------------------
// Processor Function App
// ---------------------------------------------------------------------------
resource processorFunctionApp 'Microsoft.Web/sites@2023-12-01' = {
  name: 'func-processor-${environmentName}'
  location: location
  tags: tags
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: hostingPlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v9.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: concat(commonAppSettings, [
        {
          name: 'ServiceBusConnection__fullyQualifiedNamespace'
          value: serviceBusFullyQualifiedNamespace
        }
      ])
    }
  }
}

// ---------------------------------------------------------------------------
// RBAC: Ingress → Service Bus Data Sender
// ---------------------------------------------------------------------------
resource ingressServiceBusSenderRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, ingressFunctionApp.identity.principalId, 'ServiceBusDataSender')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '69a216fc-b8fb-44d8-bc22-1f3c2cd27a39')
    principalId: ingressFunctionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// RBAC: Processor → Service Bus Data Receiver
// ---------------------------------------------------------------------------
resource processorServiceBusReceiverRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(serviceBusNamespaceId, processorFunctionApp.identity.principalId, 'ServiceBusDataReceiver')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4f6d3b9b-027b-4f4c-9142-0e5a2a2247e0')
    principalId: processorFunctionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// RBAC: Ingress → Key Vault Secrets User
// ---------------------------------------------------------------------------
resource ingressKeyVaultSecretsUserRole 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVaultId, ingressFunctionApp.identity.principalId, 'KeyVaultSecretsUser')
  scope: resourceGroup()
  properties: {
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', '4633458b-17de-408a-b874-0445c86b69e0')
    principalId: ingressFunctionApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Storage account name.')
output storageAccountName string = storageAccount.name

@description('Hosting plan name.')
output hostingPlanName string = hostingPlan.name

@description('Ingress function app name.')
output ingressFunctionAppName string = ingressFunctionApp.name

@description('Ingress function app resource ID.')
output ingressFunctionAppId string = ingressFunctionApp.id

@description('Ingress function app system-assigned managed identity principal ID.')
output ingressPrincipalId string = ingressFunctionApp.identity.principalId

@description('Processor function app name.')
output processorFunctionAppName string = processorFunctionApp.name

@description('Processor function app resource ID.')
output processorFunctionAppId string = processorFunctionApp.id

@description('Processor function app system-assigned managed identity principal ID.')
output processorPrincipalId string = processorFunctionApp.identity.principalId

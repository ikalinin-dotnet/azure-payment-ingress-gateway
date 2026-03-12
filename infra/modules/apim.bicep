@description('Azure region for all resources.')
param location string

@description('Environment name used as a suffix.')
param environmentName string

@description('Resource tags.')
param tags object = {}

@description('Publisher email for APIM notifications.')
param publisherEmail string

@description('Publisher display name.')
param publisherName string

@description('Application Insights resource ID for APIM diagnostics.')
param appInsightsId string

@description('Application Insights instrumentation key.')
param appInsightsInstrumentationKey string

// ---------------------------------------------------------------------------
// API Management (Consumption SKU)
// ---------------------------------------------------------------------------
resource apim 'Microsoft.ApiManagement/service@2023-09-01-preview' = {
  name: 'apim-payment-gateway-${environmentName}'
  location: location
  tags: tags
  sku: {
    name: 'Consumption'
    capacity: 0
  }
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    publisherEmail: publisherEmail
    publisherName: publisherName
    customProperties: {
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls10': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Protocols.Tls11': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls10': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Tls11': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Security.Backend.Protocols.Ssl30': 'False'
      'Microsoft.WindowsAzure.ApiManagement.Gateway.Protocols.Server.Http2': 'True'
    }
  }
}

// ---------------------------------------------------------------------------
// APIM Logger (Application Insights)
// ---------------------------------------------------------------------------
resource apimLogger 'Microsoft.ApiManagement/service/loggers@2023-09-01-preview' = {
  parent: apim
  name: 'appi-payment-gateway-logger'
  properties: {
    loggerType: 'applicationInsights'
    resourceId: appInsightsId
    credentials: {
      instrumentationKey: appInsightsInstrumentationKey
    }
    isBuffered: true
  }
}

// ---------------------------------------------------------------------------
// APIM Diagnostics (global)
// ---------------------------------------------------------------------------
resource apimDiagnostics 'Microsoft.ApiManagement/service/diagnostics@2023-09-01-preview' = {
  parent: apim
  name: 'applicationinsights'
  properties: {
    alwaysLog: 'allErrors'
    loggerId: apimLogger.id
    sampling: {
      samplingType: 'fixed'
      percentage: 100
    }
    logClientIp: true
    verbosity: 'information'
    httpCorrelationProtocol: 'W3C'
  }
}

// ---------------------------------------------------------------------------
// Payment Ingress API definition
// ---------------------------------------------------------------------------
resource paymentIngressApi 'Microsoft.ApiManagement/service/apis@2023-09-01-preview' = {
  parent: apim
  name: 'payment-ingress-api'
  properties: {
    displayName: 'Payment Ingress API'
    description: 'Receives inbound payment webhook payloads and forwards them to the ingress function.'
    path: 'payments'
    protocols: [
      'https'
    ]
    subscriptionRequired: true
    isCurrent: true
    apiType: 'http'
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('APIM service name.')
output apimName string = apim.name

@description('APIM resource ID.')
output apimId string = apim.id

@description('APIM gateway URL.')
output gatewayUrl string = apim.properties.gatewayUrl

@description('APIM system-assigned managed identity principal ID.')
output principalId string = apim.identity.principalId

@description('Payment Ingress API name.')
output paymentIngressApiName string = paymentIngressApi.name

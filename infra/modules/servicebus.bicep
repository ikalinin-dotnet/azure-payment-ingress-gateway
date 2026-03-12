@description('Azure region for all resources.')
param location string

@description('Environment name used as a suffix (e.g. dev, prod).')
param environmentName string

@description('Resource tags.')
param tags object = {}

// ---------------------------------------------------------------------------
// Service Bus Namespace
// ---------------------------------------------------------------------------
resource serviceBusNamespace 'Microsoft.ServiceBus/namespaces@2022-10-01-preview' = {
  name: 'sb-payment-gateway-${environmentName}'
  location: location
  tags: tags
  sku: {
    name: 'Standard'
    tier: 'Standard'
  }
  properties: {
    minimumTlsVersion: '1.2'
  }
}

// ---------------------------------------------------------------------------
// Queue: payment-ingress
// ---------------------------------------------------------------------------
resource paymentIngressQueue 'Microsoft.ServiceBus/namespaces/queues@2022-10-01-preview' = {
  parent: serviceBusNamespace
  name: 'payment-ingress'
  properties: {
    lockDuration: 'PT1M'
    maxSizeInMegabytes: 1024
    requiresDuplicateDetection: false
    requiresSession: false
    defaultMessageTimeToLive: 'P14D'
    deadLetteringOnMessageExpiration: true
    duplicateDetectionHistoryTimeWindow: 'PT10M'
    maxDeliveryCount: 10
    enablePartitioning: false
    enableExpress: false
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Service Bus Namespace name.')
output namespaceName string = serviceBusNamespace.name

@description('Service Bus Namespace resource ID.')
output namespaceId string = serviceBusNamespace.id

@description('Service Bus fully-qualified namespace (for passwordless auth).')
output fullyQualifiedNamespace string = '${serviceBusNamespace.name}.servicebus.windows.net'

@description('payment-ingress queue name.')
output queueName string = paymentIngressQueue.name

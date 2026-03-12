@description('Azure region for all resources.')
param location string

@description('Environment name used as a suffix.')
param environmentName string

@description('Resource tags.')
param tags object = {}

// ---------------------------------------------------------------------------
// Cosmos DB Account (Serverless)
// ---------------------------------------------------------------------------
resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-05-15' = {
  name: 'cosmos-payment-gateway-${environmentName}'
  location: location
  tags: tags
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
    capabilities: [
      {
        name: 'EnableServerless'
      }
    ]
    enableAutomaticFailover: false
    enableMultipleWriteLocations: false
    minimalTlsVersion: 'Tls12'
    publicNetworkAccess: 'Enabled'
  }
}

// ---------------------------------------------------------------------------
// Database: PaymentGateway
// ---------------------------------------------------------------------------
resource database 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-05-15' = {
  parent: cosmosAccount
  name: 'PaymentGateway'
  properties: {
    resource: {
      id: 'PaymentGateway'
    }
  }
}

// ---------------------------------------------------------------------------
// Container: InboundWebhooks (partition key: /provider)
// ---------------------------------------------------------------------------
resource inboundWebhooksContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-05-15' = {
  parent: database
  name: 'InboundWebhooks'
  properties: {
    resource: {
      id: 'InboundWebhooks'
      partitionKey: {
        paths: [
          '/provider'
        ]
        kind: 'Hash'
        version: 2
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        includedPaths: [
          {
            path: '/*'
          }
        ]
        excludedPaths: [
          {
            path: '/_etag/?'
          }
        ]
      }
      defaultTtl: -1
    }
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Cosmos DB account name.')
output accountName string = cosmosAccount.name

@description('Cosmos DB account resource ID.')
output accountId string = cosmosAccount.id

@description('Cosmos DB account endpoint.')
output endpoint string = cosmosAccount.properties.documentEndpoint

@description('PaymentGateway database name.')
output databaseName string = database.name

@description('InboundWebhooks container name.')
output containerName string = inboundWebhooksContainer.name

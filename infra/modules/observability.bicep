@description('Azure region for all resources.')
param location string

@description('Environment name used as a suffix.')
param environmentName string

@description('Resource tags.')
param tags object = {}

@description('Log Analytics retention in days.')
@minValue(30)
@maxValue(730)
param retentionInDays int = 90

// ---------------------------------------------------------------------------
// Log Analytics Workspace
// ---------------------------------------------------------------------------
resource logAnalyticsWorkspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: 'law-payment-gateway-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: retentionInDays
    features: {
      enableLogAccessUsingOnlyResourcePermissions: true
    }
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
  }
}

// ---------------------------------------------------------------------------
// Application Insights
// ---------------------------------------------------------------------------
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: 'appi-payment-gateway-${environmentName}'
  location: location
  tags: tags
  kind: 'web'
  properties: {
    Application_Type: 'web'
    WorkspaceResourceId: logAnalyticsWorkspace.id
    IngestionMode: 'LogAnalytics'
    publicNetworkAccessForIngestion: 'Enabled'
    publicNetworkAccessForQuery: 'Enabled'
    RetentionInDays: retentionInDays
  }
}

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Log Analytics Workspace name.')
output workspaceName string = logAnalyticsWorkspace.name

@description('Log Analytics Workspace resource ID.')
output workspaceId string = logAnalyticsWorkspace.id

@description('Application Insights name.')
output appInsightsName string = appInsights.name

@description('Application Insights resource ID.')
output appInsightsId string = appInsights.id

@description('Application Insights Instrumentation Key.')
output instrumentationKey string = appInsights.properties.InstrumentationKey

@description('Application Insights Connection String.')
output connectionString string = appInsights.properties.ConnectionString

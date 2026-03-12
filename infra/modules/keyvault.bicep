@description('Azure region for all resources.')
param location string

@description('Environment name used as a suffix.')
param environmentName string

@description('Resource tags.')
param tags object = {}

@description('Principal IDs to assign the Key Vault Secrets Officer role (e.g. CI/CD service principal).')
param secretsOfficerPrincipalIds array = []

// Key Vault Secrets Officer built-in role definition ID
var keyVaultSecretsOfficerRoleId = 'b86a8fe4-44ce-4948-aee5-eccb2c155cd7'

// ---------------------------------------------------------------------------
// Key Vault
// ---------------------------------------------------------------------------
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: 'kv-payment-gw-${environmentName}'
  location: location
  tags: tags
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 90
    enabledForDeployment: false
    enabledForDiskEncryption: false
    enabledForTemplateDeployment: true
    publicNetworkAccess: 'Enabled'
    networkAcls: {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
  }
}

// ---------------------------------------------------------------------------
// RBAC: Key Vault Secrets Officer for provided principals
// ---------------------------------------------------------------------------
resource secretsOfficerAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = [
  for principalId in secretsOfficerPrincipalIds: {
    name: guid(keyVault.id, principalId, keyVaultSecretsOfficerRoleId)
    scope: keyVault
    properties: {
      roleDefinitionId: subscriptionResourceId(
        'Microsoft.Authorization/roleDefinitions',
        keyVaultSecretsOfficerRoleId
      )
      principalId: principalId
      principalType: 'ServicePrincipal'
    }
  }
]

// ---------------------------------------------------------------------------
// Outputs
// ---------------------------------------------------------------------------
@description('Key Vault name.')
output keyVaultName string = keyVault.name

@description('Key Vault resource ID.')
output keyVaultId string = keyVault.id

@description('Key Vault URI.')
output keyVaultUri string = keyVault.properties.vaultUri

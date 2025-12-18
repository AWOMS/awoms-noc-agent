@description('Environment name (e.g., dev, prod)')
param environmentName string = 'dev'

@description('Location for all resources')
param location string = resourceGroup().location

@description('Storage account SKU')
param storageAccountSku string = 'Standard_LRS'

@description('Function App SKU')
param functionAppSku string = 'Y1'

var resourcePrefix = 'awomsnoc${environmentName}'
var storageAccountName = '${resourcePrefix}storage'
var functionAppName = '${resourcePrefix}func'
var appServicePlanName = '${resourcePrefix}plan'
var appInsightsName = '${resourcePrefix}insights'
var keyVaultName = '${resourcePrefix}kv'

// Storage Account
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = {
  name: storageAccountName
  location: location
  sku: {
    name: storageAccountSku
  }
  kind: 'StorageV2'
  properties: {
    supportsHttpsTrafficOnly: true
    minimumTlsVersion: 'TLS1_2'
    allowBlobPublicAccess: false
  }
}

// Storage Queue for alerts
resource queueService 'Microsoft.Storage/storageAccounts/queueServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}

resource alertsQueue 'Microsoft.Storage/storageAccounts/queueServices/queues@2023-01-01' = {
  name: 'alerts'
  parent: queueService
}

// Storage Tables
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-01-01' = {
  name: 'default'
  parent: storageAccount
}

resource machinesTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  name: 'machines'
  parent: tableService
}

resource telemetryTable 'Microsoft.Storage/storageAccounts/tableServices/tables@2023-01-01' = {
  name: 'telemetry'
  parent: tableService
}

// Application Insights
resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: appInsightsName
  location: location
  kind: 'web'
  properties: {
    Application_Type: 'web'
    Request_Source: 'rest'
  }
}

// App Service Plan (Consumption)
resource appServicePlan 'Microsoft.Web/serverfarms@2023-01-01' = {
  name: appServicePlanName
  location: location
  sku: {
    name: functionAppSku
    tier: 'Dynamic'
  }
  properties: {}
}

// Function App
resource functionApp 'Microsoft.Web/sites@2023-01-01' = {
  name: functionAppName
  location: location
  kind: 'functionapp'
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      netFrameworkVersion: 'v10.0'
      ftpsState: 'Disabled'
      minTlsVersion: '1.2'
      appSettings: [
        {
          name: 'AzureWebJobsStorage'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTAZUREFILECONNECTIONSTRING'
          value: 'DefaultEndpointsProtocol=https;AccountName=${storageAccount.name};AccountKey=${storageAccount.listKeys().keys[0].value};EndpointSuffix=${environment().suffixes.storage}'
        }
        {
          name: 'WEBSITE_CONTENTSHARE'
          value: toLower(functionAppName)
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
          name: 'APPINSIGHTS_INSTRUMENTATIONKEY'
          value: appInsights.properties.InstrumentationKey
        }
        {
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: appInsights.properties.ConnectionString
        }
        {
          name: 'ApiKey'
          value: '@Microsoft.KeyVault(SecretUri=${keyVault.properties.vaultUri}secrets/ApiKey/)'
        }
        {
          name: 'EmailAlerts_Enabled'
          value: 'false'
        }
        {
          name: 'EmailAlerts_To'
          value: ''
        }
        {
          name: 'TeamsAlerts_WebhookUrl'
          value: ''
        }
        {
          name: 'GenericWebhook_Url'
          value: ''
        }
      ]
    }
  }
}

// Key Vault
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: false
    accessPolicies: [
      {
        tenantId: subscription().tenantId
        objectId: functionApp.identity.principalId
        permissions: {
          secrets: [
            'get'
          ]
        }
      }
    ]
  }
}

// API Key Secret (placeholder - should be set manually)
resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  name: 'ApiKey'
  parent: keyVault
  properties: {
    value: 'change-this-to-a-secure-api-key-${uniqueString(resourceGroup().id)}'
  }
}

// Outputs
output functionAppName string = functionApp.name
output functionAppUrl string = 'https://${functionApp.properties.defaultHostName}'
output storageAccountName string = storageAccount.name
output keyVaultName string = keyVault.name
output appInsightsInstrumentationKey string = appInsights.properties.InstrumentationKey
output apiKeySecretUri string = apiKeySecret.properties.secretUri

<#
.SYNOPSIS
    Deploys the AWOMS NOC Azure infrastructure and Function App.

.DESCRIPTION
    This script deploys the Azure infrastructure using Bicep templates and provides
    instructions for deploying the Function App code.

.PARAMETER ResourceGroupName
    The name of the Azure resource group to create or use

.PARAMETER Location
    The Azure region for deployment (default: eastus)

.PARAMETER EnvironmentName
    The environment name (e.g., dev, prod) (default: dev)

.EXAMPLE
    .\Deploy-Azure.ps1 -ResourceGroupName "rg-awoms-noc" -Location "eastus" -EnvironmentName "prod"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ResourceGroupName,
    
    [Parameter(Mandatory=$false)]
    [string]$Location = "eastus",
    
    [Parameter(Mandatory=$false)]
    [string]$EnvironmentName = "dev"
)

Write-Host "Deploying AWOMS NOC Azure Infrastructure..." -ForegroundColor Green

# Check if Azure CLI is installed
try {
    az version | Out-Null
} catch {
    Write-Error "Azure CLI is not installed. Please install it from https://aka.ms/azure-cli"
    exit 1
}

# Check if logged in
$account = az account show 2>$null | ConvertFrom-Json
if (-not $account) {
    Write-Host "Not logged in to Azure. Running 'az login'..."
    az login
    $account = az account show | ConvertFrom-Json
}

Write-Host "Using subscription: $($account.name) ($($account.id))" -ForegroundColor Cyan

# Create resource group if it doesn't exist
Write-Host "`nCreating resource group: $ResourceGroupName in $Location..."
az group create --name $ResourceGroupName --location $Location | Out-Null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Resource group ready" -ForegroundColor Green
} else {
    Write-Error "Failed to create resource group"
    exit 1
}

# Deploy Bicep template
Write-Host "`nDeploying infrastructure (this may take several minutes)..."
$bicepFile = Join-Path $PSScriptRoot "..\infrastructure\main.bicep"
$parametersFile = Join-Path $PSScriptRoot "..\infrastructure\parameters.json"

if (-not (Test-Path $bicepFile)) {
    Write-Error "Bicep template not found at $bicepFile"
    exit 1
}

$deploymentOutput = az deployment group create `
    --resource-group $ResourceGroupName `
    --template-file $bicepFile `
    --parameters $parametersFile `
    --parameters environmentName=$EnvironmentName location=$Location `
    --output json | ConvertFrom-Json

if ($LASTEXITCODE -ne 0) {
    Write-Error "Deployment failed"
    exit 1
}

Write-Host "`nDeployment completed successfully!" -ForegroundColor Green

# Extract outputs
$outputs = $deploymentOutput.properties.outputs
$functionAppName = $outputs.functionAppName.value
$functionAppUrl = $outputs.functionAppUrl.value
$storageAccountName = $outputs.storageAccountName.value
$keyVaultName = $outputs.keyVaultName.value
$apiKeySecretUri = $outputs.apiKeySecretUri.value

Write-Host "`n=== Deployment Summary ===" -ForegroundColor Cyan
Write-Host "Resource Group:    $ResourceGroupName"
Write-Host "Function App:      $functionAppName"
Write-Host "Function App URL:  $functionAppUrl"
Write-Host "Storage Account:   $storageAccountName"
Write-Host "Key Vault:         $keyVaultName"
Write-Host ""

# Get API Key from Key Vault
Write-Host "Retrieving API Key from Key Vault..."
$apiKey = az keyvault secret show --vault-name $keyVaultName --name "ApiKey" --query "value" -o tsv
if ($apiKey) {
    Write-Host "API Key retrieved successfully" -ForegroundColor Green
    Write-Host ""
}

Write-Host "=== Next Steps ===" -ForegroundColor Yellow
Write-Host ""
Write-Host "1. Update the API Key in Key Vault (it's currently set to a placeholder):"
Write-Host "   az keyvault secret set --vault-name $keyVaultName --name ApiKey --value 'YOUR-SECURE-API-KEY'"
Write-Host ""
Write-Host "2. Deploy the Function App code:"
Write-Host "   cd src/AWOMS.NOC.Functions"
Write-Host "   func azure functionapp publish $functionAppName"
Write-Host ""
Write-Host "   Or use the GitHub Actions workflow to deploy automatically on push."
Write-Host ""
Write-Host "3. Configure alert settings in Function App configuration:"
Write-Host "   - EmailAlerts_Enabled: true/false"
Write-Host "   - EmailAlerts_To: your-email@domain.com"
Write-Host "   - TeamsAlerts_WebhookUrl: https://your-teams-webhook-url"
Write-Host "   - GenericWebhook_Url: https://your-webhook-url"
Write-Host ""
Write-Host "4. Install the agent on Windows machines:"
Write-Host "   .\scripts\Install-Agent.ps1 -ApiEndpoint '$functionAppUrl' -ApiKey '$apiKey'"
Write-Host ""
Write-Host "5. Monitor your deployment:"
Write-Host "   - Function App logs: https://portal.azure.com/#@/resource$($deploymentOutput.id)"
Write-Host "   - Application Insights: Check the Azure Portal"
Write-Host ""

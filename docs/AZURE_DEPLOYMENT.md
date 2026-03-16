# Manual Azure Deployment Guide

This guide walks you through creating the Azure resources required for the AWOMS NOC Agent monitoring solution using the Azure Portal.

## Prerequisites

- Azure subscription with appropriate permissions to create resources
- Existing Linux B2 App Service Plan in the target region
- Estimated incremental cost: low for Storage and Application Insights; compute is billed through the existing App Service Plan

## Step 1: Create a Resource Group

1. Sign in to the [Azure Portal](https://portal.azure.com)
2. Click **Resource groups** in the left menu
3. Click **+ Create**
4. Fill in the details:
   - **Subscription**: Select your subscription
   - **Resource group**: Enter a name (e.g., `rg-awoms-noc`)
   - **Region**: Select your preferred region (e.g., `East US`)
5. Click **Review + create**, then **Create**

## Step 2: Create a Storage Account

1. In your resource group, click **+ Create**
2. Search for "Storage account" and select it
3. Click **Create**
4. Fill in the details:
   - **Storage account name**: Enter a unique name (e.g., `awomsnoctstore` - must be globally unique, lowercase, no special characters)
   - **Region**: Same as your resource group
   - **Performance**: Standard
   - **Redundancy**: Locally-redundant storage (LRS)
5. Click **Review + create**, then **Create**
6. After deployment, go to the storage account

### Create Queue

1. In the storage account, navigate to **Data storage** → **Queues**
2. Click **+ Queue** and create:
   - Queue name: `alerts`

### Get Connection String

1. In the storage account, navigate to **Security + networking** → **Access keys**
2. Click **Show keys**
3. Copy the **Connection string** from key1 (you'll need this later)

## Step 3: Create Tables in the Storage Account

Table Storage uses the same Storage Account created in Step 2 — no separate resource needed.

1. In the storage account, navigate to **Data storage** → **Tables**
2. Click **+ Table** and create three tables:
   - Table name: `machines`
   - Table name: `machinemetrics`
   - Table name: `metrichistory`

> **Note:** The Function App will also create these tables automatically on first startup if they do not exist.

## Step 4: Create Application Insights

1. In your resource group, click **+ Create**
2. Search for "Application Insights" and select it
3. Click **Create**
4. Fill in the details:
   - **Name**: Enter a name (e.g., `awomsnoc-insights`)
   - **Region**: Same as your resource group
   - **Resource Mode**: Workspace-based
   - **Log Analytics Workspace**: Create new or select existing
5. Click **Review + create**, then **Create**
6. After deployment, go to Application Insights
7. Navigate to **Configure** → **Properties**
8. Copy the **Instrumentation Key** and **Connection String** (you'll need these later)

## Step 5: Create a Key Vault

1. In your resource group, click **+ Create**
2. Search for "Key Vault" and select it
3. Click **Create**
4. Fill in the details:
   - **Key vault name**: Enter a unique name (e.g., `awomsnoc-kv`)
   - **Region**: Same as your resource group
   - **Pricing tier**: Standard
5. Click **Review + create**, then **Create**
6. After deployment, go to the Key Vault
7. Check **Access configuration** to see whether the vault uses **Azure role-based access control** or **Vault access policy**
8. If the vault uses **Azure role-based access control** and you can't create secrets, grant yourself **Key Vault Secrets Officer** (or **Key Vault Administrator**) on the vault, then wait a few minutes for permissions to propagate
9. Navigate to **Objects** → **Secrets**
10. Click **+ Generate/Import**
11. Create a secret:
   - **Name**: `ApiKey`
   - **Value**: Generate a secure random string (e.g., using PowerShell: `[System.Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes([System.Guid]::NewGuid().ToString()))`)
12. Click **Create**
13. Click on the secret, then the current version
14. Copy the **Secret Identifier** (URI) for later use

## Step 6: Create a Function App

1. In your resource group, click **+ Create**
2. Search for "Function App" and select it
3. Click **Create**
4. Fill in the details:
   - **Function App name**: Enter a unique name (e.g., `awomsnoc-func`)
   - **Runtime stack**: .NET
   - **Version**: 10 (isolated)
   - **Region**: Same as your resource group
   - **Operating System**: Linux
   - **Plan type**: App Service Plan
   - **App Service Plan**: Select your existing Linux B2 App Service Plan
   - **Storage account**: Select the storage account created in Step 2
5. Click **Next: Networking** (keep defaults)
6. Click **Next: Monitoring**
   - **Enable Application Insights**: Yes
   - **Application Insights**: Select the one created in Step 3
7. Click **Review + create**, then **Create**

### Enable Always On

1. After deployment, go to the Function App
2. Navigate to **Settings** → **Configuration** → **General settings**
3. Set **Always On** to **On**
4. Click **Save**

> **Important:** This app uses timer and queue triggers. On an App Service Plan, **Always On** should be enabled so non-HTTP triggers continue to run reliably.

### Configure Function App Settings

1. After deployment, go to the Function App
2. Navigate to **Settings** → **Configuration**
3. Click **+ New application setting** for each of the following:

   | Name | Value |
   |------|-------|
   | `APPLICATIONINSIGHTS_CONNECTION_STRING` | Connection string from Step 4 |
   | `AzureWebJobsStorage` | Connection string from Step 2 |
   | `FUNCTIONS_EXTENSION_VERSION` | `~4` |
   | `FUNCTIONS_WORKER_RUNTIME` | `dotnet-isolated` |
   | `TableStorageConnectionString` | Connection string from Step 2 (same Storage Account) |
   | `WEBSITE_USE_PLACEHOLDER_DOTNETISOLATED` | `1`
   | `ApiKey1` | Use Key Vault reference: `@Microsoft.KeyVault(SecretUri=YOUR_SECRET_URI)` |
   | `HeartbeatTimeoutMinutes` | `5` |
   | `EmailAlerts_Enabled` | `false` (or `true` if configuring email) |
   | `SendGridApiKey` | Use Key Vault reference: `@Microsoft.KeyVault(SecretUri=YOUR_SECRET_URI)` |
   | `EmailAlerts_From` | Sender address (e.g., `noc-alerts@yourdomain.com`) |
   | `EmailAlerts_To` | Your email address |
   | `TeamsAlerts_WebhookUrl` | Your Teams webhook URL (if using) |
   | `GenericWebhook_Url` | Your webhook URL (if using) |

4. Click **Save** and **Continue**

### Enable Managed Identity and Grant Key Vault Access

1. In the Function App, navigate to **Settings** → **Identity**
2. Under **System assigned**, toggle **Status** to **On**
3. Click **Save** and **Yes** to confirm
4. Copy the **Object (principal) ID**
5. Go back to your Key Vault
6. If the vault uses **Azure role-based access control**:
   - Go to **Access control (IAM)**
   - Add the **Key Vault Secrets User** role for your Function App's managed identity
7. If the vault uses **Vault access policy**:
   - Navigate to **Access policies**
   - Click **+ Create**
   - Under **Secret permissions**, select **Get**
   - Click **Next**
   - Search for and select your Function App's managed identity (using the Object ID)
   - Click **Next**, **Next**, then **Create**

## Step 7: Deploy Function App Code

You can deploy the Function App code using one of these methods:

### Option A: GitHub Actions (Recommended)

The repository includes a GitHub Actions workflow that will automatically deploy on push to main. You need to configure:

1. In your GitHub repository, go to **Settings** → **Secrets and variables** → **Actions**
2. Add these repository secrets:
   - `AZURE_CLIENT_ID`: Your Azure service principal client ID
   - `AZURE_TENANT_ID`: Your Azure tenant ID
   - `AZURE_SUBSCRIPTION_ID`: Your Azure subscription ID
3. Add this repository variable:
   - `AZURE_FUNCTION_APP_NAME`: Your Function App name (e.g., `awomsnoc-func`)

### Option B: Azure Functions Core Tools

```powershell
# Install Azure Functions Core Tools if not already installed
# https://docs.microsoft.com/azure/azure-functions/functions-run-local

# Navigate to the Functions project
cd src/AWOMS.NOC.Functions

# Publish to Azure
func azure functionapp publish <your-function-app-name>
```

### Option C: Visual Studio / VS Code

Use the Azure Functions extension to publish directly from your IDE.

## Step 8: Verify Deployment

1. In the Azure Portal, go to your Function App
2. Navigate to **Functions**
3. You should see four functions:
   - `TelemetryIngestion`
   - `HeartbeatMonitor`
   - `AlertProcessor`
   - `MetricHistoryCleanup` (runs daily at 02:00 UTC, deletes history older than 1 year)
4. Click on `TelemetryIngestion` and note the **Function URL** (you'll need this for agent configuration)
5. Get the API key:
   - Go to your Key Vault
   - Navigate to **Secrets** → **ApiKey**
   - Click on the current version
   - Click **Show Secret Value** and copy it

## Step 9: Install Agents on Windows Machines

1. Download the latest agent release from GitHub
2. Extract to a temporary location
3. Run PowerShell as Administrator:

```powershell
.\Install-Agent.ps1 `
  -ApiEndpoint "https://your-function-app.azurewebsites.net" `
  -ApiKey "your-api-key-from-key-vault"
```

## Configuration Reference

### Agent Thresholds (appsettings.json)

The agent's `appsettings.json` includes configurable thresholds:

```json
{
  "Thresholds": {
    "DiskSpaceCriticalPercent": 10.0,
    "DiskSpaceWarningPercent": 20.0,
    "MemoryUsageCriticalPercent": 90.0,
    "MemoryUsageWarningPercent": 80.0,
    "CpuUsageCriticalPercent": 95.0,
    "CpuUsageWarningPercent": 85.0,
    "DiskQueueCritical": 3.0,
    "DiskQueueSustainedMinutes": 15,
      "HeartbeatTimeoutMinutes": 5,
      "WindowsUpdatePendingDays": 7,
      "PasswordMaxAgeDays": 31,
      "PingLatencyWarningMs": 200,
      "CriticalUpdatesPendingDays": 7
  }
}
```

### Azure Function App Settings

| Setting | Purpose | Default |
|---------|---------|---------|
| `TableStorageConnectionString` | Azure Storage Account connection string | Required |
| `HeartbeatTimeoutMinutes` | Minutes before machine marked offline | 5 |
| `EmailAlerts_Enabled` | Enable email notifications | false |
| `SendGridApiKey` | SendGrid API key for email alerts | - |
| `EmailAlerts_From` | Sender email address | - |
| `EmailAlerts_To` | Email recipient | - |
| `TeamsAlerts_WebhookUrl` | Microsoft Teams webhook | - |
| `GenericWebhook_Url` | Custom webhook endpoint | - |

## Monitoring

### View Telemetry Data

1. Go to your Storage Account
2. Navigate to **Data storage** → **Tables**
3. Open one of the three tables:
   - `machines` — one row per workstation, current heartbeat and online status
   - `machinemetrics` — one row per metric per workstation, current snapshot of all latest values
   - `metrichistory` — time-series history for trending metrics (disk space, CPU, memory, etc.), retained for 1 year

### Monitor Function Performance

1. Go to your Function App
2. Navigate to **Monitoring** → **Application Insights**
3. View logs, metrics, and traces

### Set Up Custom Alerts

1. In Application Insights, navigate to **Alerts**
2. Click **+ Create** → **Alert rule**
3. Configure alerts based on:
   - Function failures
   - Response times
   - Exception rates

## Troubleshooting

### Functions Not Receiving Telemetry

- Verify the Function App URL is correct in agent configuration
- Check API key matches between agent and Key Vault
- Review Function App logs in Application Insights
- Ensure agent can reach Azure (firewall/proxy settings)

### Alerts Not Sending

- Verify alert settings in Function App configuration
- Check `AlertProcessor` function logs
- Test webhook URLs manually
- Ensure queue messages are being created (check Storage Account queue)

### Agents Showing Offline

- Check `HeartbeatTimeoutMinutes` setting in Function App
- Verify agent service is running on machines
- Review agent logs for errors
- Check network connectivity to Azure

## Cost Optimization

- Reuse the existing Linux B2 App Service Plan if it has spare capacity
- Enable Application Insights sampling to reduce costs
- Review App Service Plan utilization before adding more workloads
- Review and adjust collection/reporting intervals in agents

## Security Best Practices

- Rotate API keys regularly
- Use managed identities where possible
- Enable Azure AD authentication for Function App
- Restrict network access using Azure Firewall or NSGs
- Enable diagnostic logging for audit trails
- Use Private Endpoints for storage accounts (if budget allows)

## Next Steps

- Configure email/Teams alerts
- Set up custom Application Insights dashboards
- Create Azure Monitor alerts for critical thresholds
- Document your organization's specific configuration

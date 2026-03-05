# AWOMS NOC Agent - Windows Workstation Monitoring Solution

A comprehensive Network Operations Center (NOC) monitoring solution for Windows 10/11 workstations and servers joined to Active Directory. The solution consists of a Windows Service agent that collects telemetry and reports to Azure Functions for off-site monitoring and alerting.

## Architecture

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           AWOMS.NOC Solution                                │
├─────────────────────────────────────────────────────────────────────────────┤
│                                                                             │
│  ┌─────────────────┐     ┌─────────────────┐     ┌─────────────────┐       │
│  │ AWOMS.NOC.Agent │     │ AWOMS.NOC.Agent │     │ AWOMS.NOC.Agent │       │
│  │  (Workstation)  │     │    (Server)     │     │  (Workstation)  │       │
│  └────────┬────────┘     └────────┬────────┘     └────────┬────────┘       │
│           │                       │                       │                 │
│           └───────────────────────┼───────────────────────┘                 │
│                                   │                                         │
│                                   ▼                                         │
│                    ┌──────────────────────────────┐                        │
│                    │   Azure Function (HTTP API)  │                        │
│                    │   AWOMS.NOC.Functions        │                        │
│                    └──────────────┬───────────────┘                        │
│                                   │                                         │
│                    ┌──────────────┴───────────────┐                        │
│                    ▼                              ▼                        │
│     ┌─────────────────────────┐    ┌─────────────────────────┐            │
│     │  Azure Table Storage    │    │   Azure Queue Storage   │            │
│     │  machines / metrics /   │    │   (Alerts)              │            │
│     │  metrichistory tables   │    └──────────┬──────────────┘            │
│     └─────────────────────────┘               │                           │
│                                               │                            │
│                                               ▼                            │
│                                ┌─────────────────────────┐                 │
│                                │  Alert Processor Func   │                 │
│                                │  (Email/Teams/Webhook)  │                 │
│                                └─────────────────────────┘                 │
│                                                                             │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Features

### Metrics Collection
- **Disk**: Free space percentage, disk queue length, total size
- **Memory**: Usage percentage, available/total memory
- **CPU**: Processor utilization percentage
- **Network**: Interface status, bytes sent/received per second, public IP detection, ping target connectivity/latency
- **System**: Last boot time, uptime, pending reboot detection
- **Windows Update**: Available updates, critical/important update count, last update install time, update age
- **Active Directory**: Domain user password age threshold monitoring
- **Security**: Antivirus and firewall status
- **Services**: Critical Windows services monitoring (DNS, Print Spooler, etc.)
- **Event Log**: Critical system and application event monitoring

### Alerting
Configurable thresholds with multi-channel alert delivery:
- Critical and Warning severity levels
- Email notifications (SMTP/SendGrid)
- Microsoft Teams integration
- Generic webhook support for custom integrations
- Heartbeat monitoring with automatic offline detection

### Infrastructure
- Cost-effective Azure consumption-based pricing (< $5/month expected)
- Secure API key authentication stored in Azure Key Vault
- Resilient with automatic retry logic and exponential backoff
- Azure Table Storage for telemetry: `machines` (heartbeat/status), `machinemetrics` (current snapshot), `metrichistory` (trending history, 1-year retention)
- Snapshot + trend-history write strategy — 150 metrics collected but only ~28 trending metrics appended per cycle
- Queue Storage for reliable alert delivery

## Prerequisites

### For Azure Deployment
- Azure subscription
- Azure CLI installed ([Download](https://aka.ms/azure-cli))
- .NET 10 SDK ([Download](https://dotnet.microsoft.com/download/dotnet/10.0))
- PowerShell 7+ (recommended)

### For Agent Installation
- Windows 10/11 or Windows Server 2016+
- Administrator privileges
- Network access to Azure (outbound HTTPS to your Function App)

## Quick Start

### 1. Deploy Azure Infrastructure

Follow the step-by-step manual deployment guide:

📖 **[Azure Deployment Guide](docs/AZURE_DEPLOYMENT.md)**

The guide walks you through creating all required Azure resources using the Azure Portal:
- Resource Group
- Storage Account (for queues and Table Storage)
- Application Insights
- Key Vault
- Function App

After completing the deployment, you'll have:
- Your Function App URL
- API Key stored in Key Vault

### 2. Deploy Function App Code

#### Option A: Using Azure Functions Core Tools
```powershell
cd src/AWOMS.NOC.Functions
func azure functionapp publish <your-function-app-name>
```

#### Option B: Using GitHub Actions
Push to the `main` branch and the workflow will automatically deploy.

### 3. Install Agent on Windows Machines

Download the latest release from the [Releases page](https://github.com/AWOMS/awoms-noc-agent/releases) or build from source.

```powershell
# Extract the release ZIP
Expand-Archive -Path AWOMS.NOC.Agent-win-x64.zip -DestinationPath C:\Temp\NOCAgent

# Run as Administrator
cd C:\Temp\NOCAgent
.\Install-Agent.ps1 -ApiEndpoint "https://your-function-app.azurewebsites.net" -ApiKey "your-api-key"
```

## Configuration

### Agent Configuration

Edit `appsettings.json` to customize agent behavior:

```json
{
  "AgentConfiguration": {
    "ApiEndpoint": "https://your-function-app.azurewebsites.net",
    "ApiKey": "your-api-key-here",
    "CollectionIntervalSeconds": 60,
    "ReportingIntervalSeconds": 300,
    "EnableLocalAlerts": true,
    "PublicIpUrl": "https://api.ipify.org/",
    "PingTargets": ["8.8.8.8", "1.1.1.1"],
    "MonitoredServices": ["Dnscache", "LanmanServer", "LanmanWorkstation", "Spooler", "W32Time", "WinDefend"]
  }
}
```

| Setting | Description | Default |
|---------|-------------|---------|
| `ApiEndpoint` | Azure Function App URL | Required |
| `ApiKey` | Authentication key | Required |
| `CollectionIntervalSeconds` | How often to collect metrics | 60 |
| `ReportingIntervalSeconds` | How often to send telemetry | 300 |
| `EnableLocalAlerts` | Evaluate alerts locally for immediate critical alerts | true |
| `PublicIpUrl` | URL used to resolve external IP | https://api.ipify.org/ |
| `PingTargets` | Targets used for connectivity/latency checks | 8.8.8.8, 1.1.1.1 |
| `MonitoredServices` | Windows service names monitored for stopped state | DNS/Spooler/etc |

### Alert Thresholds

Thresholds are configured in the agent's `appsettings.json` file and can be customized per deployment:

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

Default threshold values:

| Metric | Warning | Critical | Notes |
|--------|---------|----------|-------|
| Disk Free Space | < 20% | < 10% | Per drive |
| Memory Usage | > 80% | > 90% | System-wide |
| CPU Usage | > 85% | > 95% | Sustained for 10 min |
| Disk Queue Length | N/A | > 3 | Sustained for 15 min |
| Heartbeat Timeout | N/A | > 5 minutes | Machine offline |
| Windows Updates | Available critical/important updates | > 7 days | Since last successful update |
| AD Password Age | > 31 days | N/A | Per domain user |
| Ping Latency | > 200ms | N/A | Per configured target |
| Ping Status | N/A | Not Success | Per configured target |
| Antivirus Status | Outdated | Disabled | Security risk |
| Critical Services | N/A | Stopped | Service failure |

To customize thresholds, edit the agent's `appsettings.json` before installation or update it on deployed agents.

### Alert Channel Configuration

Configure alert channels in Azure Function App settings (see the [Azure Deployment Guide](docs/AZURE_DEPLOYMENT.md) for details):

- **Email Alerts**: Set `EmailAlerts_Enabled=true`, `EmailAlerts_From`, `EmailAlerts_To`, and `SendGridApiKey`
- **Teams Alerts**: Set `TeamsAlerts_WebhookUrl` with your webhook URL
- **Generic Webhook**: Set `GenericWebhook_Url` for custom integrations
- **Heartbeat Timeout**: Set `HeartbeatTimeoutMinutes` (default: 5)

## Monitoring and Alerting

### View Telemetry Data

Query Azure Cosmos DB using Data Explorer or Azure Portal:

**Machines Container**: Current status of all monitored machines
- Partition Key: `/agentId`
- Document id: `AgentId`

**Telemetry Container**: Historical metrics
- Partition Key: `/agentId`
- Document id: `{AgentId}_{EncodedMetricKey}`

### Application Insights

Monitor Function App performance and errors:
```powershell
# View recent logs
az monitor app-insights query --app <app-insights-name> --analytics-query "traces | take 50"
```

### Alert Channels

**Email**: Configure SMTP or SendGrid
```json
"EmailAlerts_Enabled": "true",
"EmailAlerts_From": "noc-alerts@yourdomain.com",
"EmailAlerts_To": "alerts@yourdomain.com",
"SendGridApiKey": "your-sendgrid-key"
```

**Microsoft Teams**: Create an incoming webhook
1. Navigate to your Teams channel
2. Click "..." → Connectors → Incoming Webhook
3. Copy webhook URL to `TeamsAlerts_WebhookUrl`

**Custom Webhook**: POST JSON payload to your endpoint
```json
{
  "alertId": "guid",
  "machineName": "DESKTOP-ABC123",
  "severity": "Critical",
  "category": "Disk",
  "message": "Disk free space critically low: 8.5%",
  "timestamp": "2024-01-15T10:30:00Z"
}
```

## Troubleshooting

### Agent Issues

**Service won't start**
1. Check Event Viewer → Windows Logs → Application
2. Verify `appsettings.json` is properly formatted
3. Ensure API endpoint is accessible: `Test-NetConnection your-function-app.azurewebsites.net -Port 443`

**No telemetry being sent**
1. Check agent logs in installation directory
2. Verify API key matches Key Vault secret
3. Test connectivity to Azure Function App
4. Check Windows Firewall rules

**High CPU/Memory usage**
- Adjust `CollectionIntervalSeconds` to reduce frequency
- Check for performance counter access issues (requires admin rights)

### Function App Issues

**401 Unauthorized errors**
- Verify API key in Key Vault matches agent configuration
- Check Function App has access to Key Vault (Managed Identity)

**500 Internal Server errors**
- Check Application Insights for exceptions
- Verify `AzureWebJobsStorage` and `CosmosDbConnectionString` are valid
- Ensure Cosmos DB database/containers (`awomsnoc`, `machines`, `telemetry`) exist

**No alerts being sent**
- Verify alert configuration in Function App settings
- Check AlertProcessor function logs in Application Insights
- Test webhook URLs manually

### Common Issues

**Performance Counters not available (Linux/Docker)**
- The agent is designed for Windows only
- Performance counters require Windows OS

**Access denied errors**
- Ensure service runs with sufficient privileges
- Some metrics (registry, WMI) require administrator access

## Network Requirements

The agent requires outbound HTTPS (port 443) access to:
- `*.azurewebsites.net` (your Function App)
- `*.table.core.windows.net` (Azure Table Storage)
- `*.queue.core.windows.net` (Azure Queue Storage)

Ensure your firewall allows these connections from your VLANs.

## Cost Estimates

Based on 15 machines reporting every 5 minutes:
- Azure Functions (Consumption): ~$1/month
- Storage (Tables + Queue): <$1/month
- Application Insights: ~$1/month (with sampling)
- **Total: < $2/month**

Costs scale linearly with machine count and reporting frequency.

## Security Considerations

- ✅ API keys stored in Azure Key Vault
- ✅ HTTPS-only communication
- ✅ Managed Identity for Function App
- ✅ No credentials stored on agent machines
- ✅ Minimal required permissions for service account
- ⚠️ Rotate API keys regularly
- ⚠️ Use Azure Private Link for enhanced security (optional)

## Contributing

Contributions are welcome! For development setup, testing, commit guidelines, and architecture details, see [CONTRIBUTING.md](CONTRIBUTING.md).

## License

Copyright © 2024 AWOMS. All rights reserved.

This software is proprietary and confidential. Unauthorized copying, distribution, or use is strictly prohibited.

## Support

For issues, questions, or feature requests:
- Open an issue on [GitHub](https://github.com/AWOMS/awoms-noc-agent/issues)
- Contact: support@awoms.com

## Changelog

📜 See [CHANGELOG.md](CHANGELOG.md) for release history and version updates.

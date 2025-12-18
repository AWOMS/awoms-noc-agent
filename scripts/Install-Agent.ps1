<#
.SYNOPSIS
    Installs the AWOMS NOC Agent as a Windows Service.

.DESCRIPTION
    This script installs the AWOMS NOC Agent, configures it, and registers it as a Windows Service.

.PARAMETER ApiEndpoint
    The Azure Function App endpoint URL (e.g., https://your-function-app.azurewebsites.net)

.PARAMETER ApiKey
    The API key for authenticating with the Azure Function App

.PARAMETER InstallPath
    The installation path for the agent (default: C:\Program Files\AWOMS\NOC.Agent)

.EXAMPLE
    .\Install-Agent.ps1 -ApiEndpoint "https://awomsnoc.azurewebsites.net" -ApiKey "your-api-key"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)]
    [string]$ApiEndpoint,
    
    [Parameter(Mandatory=$true)]
    [string]$ApiKey,
    
    [Parameter(Mandatory=$false)]
    [string]$InstallPath = "C:\Program Files\AWOMS\NOC.Agent"
)

# Check if running as Administrator
$currentPrincipal = New-Object Security.Principal.WindowsPrincipal([Security.Principal.WindowsIdentity]::GetCurrent())
if (-not $currentPrincipal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Error "This script must be run as Administrator"
    exit 1
}

$serviceName = "AWOMSNOCAgent"
$serviceDisplayName = "AWOMS NOC Agent"
$serviceDescription = "AWOMS Network Operations Center monitoring agent"

Write-Host "Installing AWOMS NOC Agent..." -ForegroundColor Green

# Create installation directory
if (-not (Test-Path $InstallPath)) {
    Write-Host "Creating installation directory: $InstallPath"
    New-Item -ItemType Directory -Path $InstallPath -Force | Out-Null
}

# Copy agent files
Write-Host "Copying agent files..."
$sourceFiles = Get-ChildItem -Path $PSScriptRoot -File
foreach ($file in $sourceFiles) {
    if ($file.Name -notin @("Install-Agent.ps1", "Uninstall-Agent.ps1")) {
        Copy-Item -Path $file.FullName -Destination $InstallPath -Force
        Write-Host "  Copied: $($file.Name)"
    }
}

# Update appsettings.json
$appsettingsPath = Join-Path $InstallPath "appsettings.json"
if (Test-Path $appsettingsPath) {
    Write-Host "Updating configuration..."
    $appsettings = Get-Content $appsettingsPath -Raw | ConvertFrom-Json
    $appsettings.AgentConfiguration.ApiEndpoint = $ApiEndpoint
    $appsettings.AgentConfiguration.ApiKey = $ApiKey
    $appsettings | ConvertTo-Json -Depth 10 | Set-Content $appsettingsPath
    Write-Host "  Configuration updated"
} else {
    Write-Warning "appsettings.json not found at $appsettingsPath"
}

# Check if service already exists
$existingService = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($existingService) {
    Write-Host "Service already exists. Stopping and removing..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    sc.exe delete $serviceName
    Start-Sleep -Seconds 2
}

# Register the service
Write-Host "Registering Windows Service..."
$exePath = Join-Path $InstallPath "AWOMS.NOC.Agent.exe"
if (-not (Test-Path $exePath)) {
    Write-Error "Agent executable not found at $exePath"
    exit 1
}

# Create the service
sc.exe create $serviceName binPath= "$exePath" start= auto DisplayName= "$serviceDisplayName"
if ($LASTEXITCODE -ne 0) {
    Write-Error "Failed to create service"
    exit 1
}

# Set service description
sc.exe description $serviceName "$serviceDescription"

# Configure service recovery options (restart on failure)
sc.exe failure $serviceName reset= 86400 actions= restart/60000/restart/60000/restart/60000

# Start the service
Write-Host "Starting service..."
Start-Service -Name $serviceName

# Wait a moment and check status
Start-Sleep -Seconds 3
$service = Get-Service -Name $serviceName
if ($service.Status -eq "Running") {
    Write-Host "`nAWOMS NOC Agent installed and started successfully!" -ForegroundColor Green
    Write-Host "Service Name: $serviceName"
    Write-Host "Installation Path: $InstallPath"
    Write-Host "API Endpoint: $ApiEndpoint"
    Write-Host "`nYou can manage the service using:"
    Write-Host "  - Services Management Console (services.msc)"
    Write-Host "  - PowerShell: Get-Service $serviceName"
    Write-Host "  - Command Prompt: sc query $serviceName"
} else {
    Write-Warning "Service was created but is not running. Status: $($service.Status)"
    Write-Host "Check Event Viewer or logs for errors."
    exit 1
}

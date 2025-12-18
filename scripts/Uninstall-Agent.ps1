<#
.SYNOPSIS
    Uninstalls the AWOMS NOC Agent Windows Service.

.DESCRIPTION
    This script stops and removes the AWOMS NOC Agent service and optionally removes installation files.

.PARAMETER RemoveFiles
    If specified, removes all agent files from the installation directory

.PARAMETER InstallPath
    The installation path of the agent (default: C:\Program Files\AWOMS\NOC.Agent)

.EXAMPLE
    .\Uninstall-Agent.ps1 -RemoveFiles
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory=$false)]
    [switch]$RemoveFiles,
    
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

Write-Host "Uninstalling AWOMS NOC Agent..." -ForegroundColor Yellow

# Check if service exists
$service = Get-Service -Name $serviceName -ErrorAction SilentlyContinue
if ($service) {
    Write-Host "Stopping service..."
    Stop-Service -Name $serviceName -Force -ErrorAction SilentlyContinue
    Start-Sleep -Seconds 2
    
    Write-Host "Removing service..."
    sc.exe delete $serviceName
    if ($LASTEXITCODE -eq 0) {
        Write-Host "Service removed successfully"
    } else {
        Write-Warning "Failed to remove service (exit code: $LASTEXITCODE)"
    }
} else {
    Write-Host "Service not found, skipping service removal"
}

# Remove files if requested
if ($RemoveFiles) {
    if (Test-Path $InstallPath) {
        Write-Host "Removing installation files from $InstallPath..."
        try {
            Remove-Item -Path $InstallPath -Recurse -Force
            Write-Host "Installation files removed successfully"
        } catch {
            Write-Warning "Failed to remove some files: $_"
        }
    } else {
        Write-Host "Installation path not found: $InstallPath"
    }
}

Write-Host "`nAWOMS NOC Agent uninstalled successfully!" -ForegroundColor Green

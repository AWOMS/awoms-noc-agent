# Contributing to AWOMS NOC Agent

Thanks for helping improve the NOC agent! This guide explains how to propose changes in a way that keeps the project healthy and easy to review.

## Quick Start
1. Fork the repo and create a feature branch from `main`.
2. Keep each branch focused on a single logical change.
3. Run the relevant tests or builds (see below) before opening a pull request.
4. Use Conventional Commit messages (details in `.github/copilot-instructions.md`).

## Testing & Validation
- Build the Azure Functions project with `dotnet build src/AWOMS.NOC.Functions`.
- Run the agent locally via `dotnet run --project src/AWOMS.NOC.Agent` when touching agent code.
- Add or update unit tests when changing business logic or shared models.
- For telemetry collectors, include sample payloads or describe validation steps in the PR.

## Development Setup

```powershell
# Clone repository
git clone https://github.com/AWOMS/awoms-noc-agent.git
cd awoms-noc-agent

# Restore and build
dotnet restore
dotnet build

# Run agent locally (not as service)
cd src/AWOMS.NOC.Agent
dotnet run

# Run Functions locally
cd src/AWOMS.NOC.Functions
func start
```

## Local Testing

```powershell
# Build agent for Windows
dotnet publish src/AWOMS.NOC.Agent/AWOMS.NOC.Agent.csproj -c Release -r win-x64 --self-contained

# Test Functions locally with Azurite
# Install Azurite: npm install -g azurite
azurite --silent --location ./azurite --debug ./azurite/debug.log
cd src/AWOMS.NOC.Functions
func start
```

## Project Structure

```
awoms-noc-agent/
├── .github/workflows/          # CI/CD pipelines
│   ├── build-agent.yml         # Build and release agent
│   └── deploy-functions.yml    # Deploy Azure Functions
├── docs/                       # Documentation
│   └── AZURE_DEPLOYMENT.md     # Manual Azure deployment guide
├── scripts/                    # PowerShell scripts
│   ├── Install-Agent.ps1       # Agent installer
│   └── Uninstall-Agent.ps1     # Agent uninstaller
├── src/
│   ├── AWOMS.NOC.Shared/       # Shared models and constants
│   ├── AWOMS.NOC.Agent/        # Windows Service agent
│   └── AWOMS.NOC.Functions/    # Azure Functions
└── AWOMS.NOC.sln               # Solution file
```

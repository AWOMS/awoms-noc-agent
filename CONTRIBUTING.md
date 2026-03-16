# Contributing to AWOMS NOC Agent

Thank you for your interest in contributing! This guide explains how to set up a development environment and make contributions.

## Code of Conduct

Be respectful, inclusive, and constructive in all interactions.

## Development Setup

### Building from Source

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

### Testing

```powershell
# Build agent for Windows
dotnet publish src/AWOMS.NOC.Agent/AWOMS.NOC.Agent.csproj -c Release -r win-x64 --self-contained

# Test Functions locally with Azurite + Cosmos DB Emulator
# Install Azurite: npm install -g azurite
azurite --silent --location ./azurite --debug ./azurite/debug.log

# Start Cosmos DB Emulator (Docker) separately
# docker run --name cosmos -p 8081:8081 -m 3g mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator

cd src/AWOMS.NOC.Functions
func start
```

### Project Structure

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

## Commit Guidelines

All commits **must** follow the [Conventional Commits](https://www.conventionalcommits.org/) specification:

```
<type>(<scope>): <description>
```

**Types**: `feat`, `fix`, `chore`, `docs`, `refactor`, `perf`, `test`, `build`, `ci`, `revert`

**Scope**: Primary area touched (e.g., `agent`, `functions`, `shared`, `docs`; use `core` if uncertain)

**Description**: Start with a verb in present tense, keep under 60 characters, no ending punctuation.

### Examples

```
feat(agent): add debug logging for metric collection
fix(functions): handle missing partition key in snapshot upsert
docs(deployment): update table storage prerequisites
test(agent): add AlertEvaluator threshold boundary tests
```

## Before Submitting a Pull Request

### 1. Validate locally

```powershell
# Build the solution
dotnet build AWOMS.NOC.sln

# Run all tests
dotnet test AWOMS.NOC.sln

# Check for lint errors (if applicable)
dotnet format --verify-no-changes --verbosity diagnostic
```

### 2. Follow the code style

- Use C# 10 features when advantageous
- Follow [Microsoft C# Coding Conventions](https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/coding-style/coding-conventions)
- Use meaningful variable/method names
- Add XML documentation comments for public APIs

### 3. Test your changes

- Add unit tests for new logic
- Ensure all existing tests pass
- Test edge cases and error scenarios
- For agent changes: test on Windows before submitting

### 4. Update documentation

- Update relevant docs if behavior changes
- Add comments for complex logic
- Update `AZURE_DEPLOYMENT.md` if deployment steps change
- Update configuration tables if new settings are added

## Pull Request Process

1. **Create a feature branch** from `main`:
   ```powershell
   git checkout -b feat/your-feature
   ```

2. **Make atomic commits** split by logical unit (not all changes in one commit):
   ```powershell
   git commit -m "feat(scope): add feature x"
   git commit -m "test(scope): add tests for feature x"
   ```

3. **Push to your fork** and open a PR:
   - Write a clear PR title (also use conventional commit format)
   - Link any related issues
   - Describe what changed and why
   - List any breaking changes

4. **Address feedback** promptly

5. **Squash only if necessary** before merge

## Architecture

### Agent (`AWOMS.NOC.Agent`)

- **Worker.cs**: Main background service loop, two periodic timers for collection and reporting
- **Collectors/**: Pluggable metric collection classes implementing `IMetricCollector`
- **Services/**: `AlertEvaluator` (threshold checks), `TelemetryService` (HTTP client)
- Configuration via `appsettings.json`

### Functions (`AWOMS.NOC.Functions`)

- **Program.cs**: DI setup, table storage initialization
- **TelemetryIngestion.cs**: HTTP endpoint, validates requests, upserts machine status, writes metrics to snapshot + history tables
- **HeartbeatMonitor.cs**: Timer trigger every 5 min, checks timeout, updates online status
- **AlertProcessor.cs**: Queue trigger, sends alerts via email/Teams/webhook
- **MetricHistoryCleanup.cs**: Daily timer, deletes history older than 1 year

### Shared (`AWOMS.NOC.Shared`)

- **Models/**: DTOs and entity classes for agent, metrics, alerts, tables
- **Constants.cs**: Central configuration constants

## Common Tasks

### Adding a new metric

1. Create a collector in `src/AWOMS.NOC.Agent/Collectors/YourCollector.cs`
2. Implement `IMetricCollector.CollectAsync()` returning `List<MetricData>`
3. Register in `Program.cs`: `builder.Services.AddSingleton<IMetricCollector, YourCollector>()`
4. Add thresholds to `ThresholdsConfiguration.cs` if needed
5. Add alert logic to `AlertEvaluator.cs`
6. Add unit tests

### Adding a trending metric to history

Edit `TelemetryIngestion.cs` and add the metric prefix to `TrendingMetricPrefixes`:

```csharp
private static readonly string[] TrendingMetricPrefixes =
[
    "YourCategory|Your Metric Name",
    // ... existing prefixes
];
```

### Changing alert thresholds

1. Update `ThresholdsConfiguration.cs` property
2. Update both `appsettings.json` files if defaults change
3. Update README.md threshold table
4. Add tests if logic changed

### Updating Azure deployment steps

1. Edit `docs/AZURE_DEPLOYMENT.md`
2. Update the table of settings if new env vars added
3. Test the steps manually if possible
4. Update cost estimates if applicable

## Testing in Production-like Environment

For testing locally with Azure Storage Emulator:

```powershell
# Install Azure Storage Emulator
# https://learn.microsoft.com/en-us/azure/storage/common/storage-use-azurite?tabs=docker-hub

# Start Azurite (Table Storage + Queue)
azurite --silent

# Update local.settings.json
# "AzureWebJobsStorage": "UseDevelopmentStorage=true"
# "TableStorageConnectionString": "UseDevelopmentStorage=true"

# Start Functions
cd src/AWOMS.NOC.Functions
func start
```

Then POST test telemetry to `http://localhost:7071/api/telemetry`.

## Reporting Issues

- Use GitHub Issues for bugs and feature requests
- Provide:
  - OS version (e.g., Windows 11 23H2)
  - .NET version (output of `dotnet --version`)
  - Steps to reproduce
  - Expected vs actual behavior
  - Relevant logs or error messages

## Recognition

Contributors will be:
- Credited in the `CHANGELOG.md` for significant work
- Added to this file's contributors section (upon request)

Thank you for making AWOMS NOC better!

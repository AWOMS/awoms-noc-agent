using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Collectors;
using AWOMS.NOC.Agent.Services;
using Polly;

var builder = Host.CreateApplicationBuilder(args);

// Add Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AWOMS NOC Agent";
});

// Configure AgentConfiguration from appsettings.json
var agentConfig = new AgentConfiguration();
builder.Configuration.GetSection("AgentConfiguration").Bind(agentConfig);
builder.Services.AddSingleton(agentConfig);

// Register collectors
builder.Services.AddSingleton<IMetricCollector, CpuMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, DiskMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, MemoryMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, NetworkMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, SystemMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, SecurityMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, ServiceMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, EventLogMetricCollector>();

// Register services
builder.Services.AddSingleton<AlertEvaluator>();

// Configure HTTP client with Polly retry policy
builder.Services.AddHttpClient<TelemetryService>()
    .AddPolicyHandler(TelemetryService.GetRetryPolicy());

// Add Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Collectors;
using AWOMS.NOC.Agent.Services;
using Polly;
using Serilog;

var builder = Host.CreateApplicationBuilder(args);

const string outputTemplate =
    "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{Caller}] {Message:lj}{NewLine}{Exception}";

Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.With<CallerEnricher>()
    .WriteTo.Console(outputTemplate: outputTemplate)
    .WriteTo.File(
        path: @"C:\AWOMS\Logs\AWOMS.NOC.Agent\agent-.log",
        outputTemplate: outputTemplate,
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 14,
        shared: true)
    .CreateLogger();

builder.Services.AddSerilog();

// Add Windows Service support
builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "AWOMS NOC Agent";
});

// Configure AgentConfiguration from appsettings.json
var agentConfig = new AgentConfiguration();
builder.Configuration.GetSection("AgentConfiguration").Bind(agentConfig);
builder.Services.AddSingleton(agentConfig);

// Configure Thresholds from appsettings.json
var thresholdsConfig = new ThresholdsConfiguration();
builder.Configuration.GetSection("Thresholds").Bind(thresholdsConfig);
builder.Services.AddSingleton(thresholdsConfig);

// Register collectors
builder.Services.AddSingleton<IMetricCollector, CpuMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, DiskMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, MemoryMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, NetworkMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, SystemMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, SecurityMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, ServiceMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, EventLogMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, ActiveDirectoryMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, WindowsUpdateMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, NetworkConnectivityMetricCollector>();
builder.Services.AddSingleton<IMetricCollector, PublicIpMetricCollector>();

// Register services
builder.Services.AddSingleton<AlertEvaluator>();

// Configure HTTP client with Polly retry policy
builder.Services.AddHttpClient<TelemetryService>()
    .AddPolicyHandler(TelemetryService.GetRetryPolicy());

// Add Worker
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();

using AWOMS.NOC.Agent.Collectors;
using AWOMS.NOC.Agent.Services;
using AWOMS.NOC.Shared.Models;
using System.Net;
using System.Net.NetworkInformation;

namespace AWOMS.NOC.Agent;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly IEnumerable<IMetricCollector> _collectors;
    private readonly TelemetryService _telemetryService;
    private readonly AlertEvaluator _alertEvaluator;
    private readonly AgentConfiguration _configuration;
    private readonly string _agentId;
    private readonly string _machineName;
    private readonly string _domainName;
    private List<MetricData> _collectedMetrics = new();

    public Worker(
        ILogger<Worker> logger,
        IEnumerable<IMetricCollector> collectors,
        TelemetryService telemetryService,
        AlertEvaluator alertEvaluator,
        AgentConfiguration configuration)
    {
        _logger = logger;
        _collectors = collectors;
        _telemetryService = telemetryService;
        _alertEvaluator = alertEvaluator;
        _configuration = configuration;
        
        _machineName = Environment.MachineName;
        _domainName = Environment.UserDomainName;
        _agentId = GenerateAgentId(_machineName, _domainName);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("AWOMS NOC Agent started. AgentId: {AgentId}", _agentId);

        var collectionTimer = new PeriodicTimer(TimeSpan.FromSeconds(_configuration.CollectionIntervalSeconds));
        var reportingTimer = new PeriodicTimer(TimeSpan.FromSeconds(_configuration.ReportingIntervalSeconds));
        
        var collectionTask = CollectMetricsLoop(collectionTimer, stoppingToken);
        var reportingTask = ReportMetricsLoop(reportingTimer, stoppingToken);

        await Task.WhenAll(collectionTask, reportingTask);
    }

    private async Task CollectMetricsLoop(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var metrics = new List<MetricData>();
                
                foreach (var collector in _collectors)
                {
                    try
                    {
                        var collectorMetrics = await collector.CollectAsync();
                        metrics.AddRange(collectorMetrics);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error collecting metrics from {CollectorType}", collector.GetType().Name);
                    }
                }

                lock (_collectedMetrics)
                {
                    _collectedMetrics.AddRange(metrics);
                }

                // Check for critical alerts immediately
                if (_configuration.EnableLocalAlerts)
                {
                    var alerts = _alertEvaluator.EvaluateMetrics(metrics, _agentId, _machineName);
                    var criticalAlerts = alerts.Where(a => a.Severity == "Critical").ToList();
                    
                    if (criticalAlerts.Any())
                    {
                        _logger.LogWarning("Critical alerts detected: {Count}", criticalAlerts.Count);
                        // Send immediately
                        await SendTelemetryWithAlerts(metrics, criticalAlerts);
                    }
                }

                _logger.LogInformation("Collected {Count} metrics", metrics.Count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metric collection loop");
            }
        }
    }

    private async Task ReportMetricsLoop(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                List<MetricData> metricsToSend;
                lock (_collectedMetrics)
                {
                    metricsToSend = new List<MetricData>(_collectedMetrics);
                    _collectedMetrics.Clear();
                }

                if (metricsToSend.Any())
                {
                    var alerts = _alertEvaluator.EvaluateMetrics(metricsToSend, _agentId, _machineName);
                    await SendTelemetryWithAlerts(metricsToSend, alerts);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in reporting loop");
            }
        }
    }

    private async Task SendTelemetryWithAlerts(List<MetricData> metrics, List<AlertData> alerts)
    {
        var payload = new TelemetryPayload
        {
            AgentId = _agentId,
            MachineName = _machineName,
            DomainName = _domainName,
            IpAddress = GetLocalIpAddress(),
            OsVersion = Environment.OSVersion.ToString(),
            Timestamp = DateTime.UtcNow,
            Metrics = metrics,
            Alerts = alerts
        };

        var success = await _telemetryService.SendTelemetryAsync(payload);
        
        if (success)
        {
            _logger.LogInformation("Sent {MetricCount} metrics and {AlertCount} alerts", 
                metrics.Count, alerts.Count);
        }
        else
        {
            _logger.LogWarning("Failed to send telemetry");
        }
    }

    private string GetLocalIpAddress()
    {
        try
        {
            var host = Dns.GetHostEntry(Dns.GetHostName());
            var ipAddress = host.AddressList
                .FirstOrDefault(ip => ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
            return ipAddress?.ToString() ?? "Unknown";
        }
        catch
        {
            return "Unknown";
        }
    }

    private string GenerateAgentId(string machineName, string domainName)
    {
        // Generate a stable agent ID based on machine name and domain
        var combined = $"{domainName}\\{machineName}".ToLowerInvariant();
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(combined))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

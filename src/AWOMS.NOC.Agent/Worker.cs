using AWOMS.NOC.Agent.Collectors;
using AWOMS.NOC.Agent.Services;
using AWOMS.NOC.Shared.Models;
using System.Diagnostics;
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
        _logger.LogInformation(
            "AWOMS NOC Agent started on {MachineName} ({DomainName}). AgentId: {AgentId}. " +
            "Collecting every {CollectionInterval}s, reporting every {ReportingInterval}s to {ApiEndpoint}",
            _machineName,
            _domainName,
            _agentId,
            _configuration.CollectionIntervalSeconds,
            _configuration.ReportingIntervalSeconds,
            _configuration.ApiEndpoint);

        try
        {
            var collectionTimer = new PeriodicTimer(TimeSpan.FromSeconds(_configuration.CollectionIntervalSeconds));
            var reportingTimer = new PeriodicTimer(TimeSpan.FromSeconds(_configuration.ReportingIntervalSeconds));

            var collectionTask = CollectMetricsLoop(collectionTimer, stoppingToken);
            var reportingTask = ReportMetricsLoop(reportingTimer, stoppingToken);

            await Task.WhenAll(collectionTask, reportingTask);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("AWOMS NOC Agent stopping");
        }
        catch (Exception ex)
        {
            _logger.LogCritical(ex, "Fatal error in worker execution. Exiting process for service recovery.");
            Environment.Exit(1);
        }
    }

    private async Task CollectMetricsLoop(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var waitSeconds = _configuration.CollectionIntervalSeconds;
            var nextExecutionUtc = DateTimeOffset.UtcNow.AddSeconds(waitSeconds);
            _logger.LogInformation(
                "Waiting {WaitSeconds}s — next collection at {NextTime:HH:mm:ss} UTC",
                waitSeconds,
                nextExecutionUtc);

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }

            try
            {
                var metrics = new List<MetricData>();
                var sw = Stopwatch.StartNew();
                var collectorCount = 0;

                foreach (var collector in _collectors)
                {
                    try
                    {
                        var collectorMetrics = await collector.CollectAsync();
                        metrics.AddRange(collectorMetrics);
                        collectorCount++;

_logger.LogDebug("Collected {Count} metrics from {CollectorType}", collectorMetrics.Count, collector.GetType().Name);
                        if (_logger.IsEnabled(LogLevel.Debug))
                        {
                            foreach (var metric in collectorMetrics)
                            {
                                _logger.LogDebug(
                                    "Collected metric from {CollectorType}: {Category}.{Name}={Value} {Unit} at {Timestamp:O}",
                                    collector.GetType().Name,
                                    metric.Category,
                                    metric.Name,
                                    metric.Value,
                                    metric.Unit,
                                    metric.Timestamp);
                            }
                        }
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
                        var alertSummary = string.Join(", ", criticalAlerts.Select(a => $"{a.Category}/{a.MetricName}"));
                        _logger.LogWarning(
                            "Critical alerts detected — sending {Count} immediately: {AlertSummary}",
                            criticalAlerts.Count,
                            alertSummary);
                        // Send immediately
                        await SendTelemetryWithAlerts(metrics, criticalAlerts);
                    }
                }

                sw.Stop();
                _logger.LogInformation(
                    "Metric collection complete: {MetricCount} metrics from {CollectorCount} collectors in {ElapsedMs}ms",
                    metrics.Count,
                    collectorCount,
                    sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in metric collection loop");
            }
        }
    }

    private async Task ReportMetricsLoop(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var waitSeconds = _configuration.ReportingIntervalSeconds;
            var nextExecutionUtc = DateTimeOffset.UtcNow.AddSeconds(waitSeconds);
            _logger.LogInformation(
                "Waiting {WaitSeconds}s — next telemetry report at {NextTime:HH:mm:ss} UTC",
                waitSeconds,
                nextExecutionUtc);

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }

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
                else
                {
                    _logger.LogInformation("Reporting cycle skipped — no metrics queued");
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
            _logger.LogInformation(
                "Telemetry sent for {MachineName}: {MetricCount} metrics, {AlertCount} alerts",
                _machineName,
                metrics.Count,
                alerts.Count);
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

    internal static string GenerateAgentId(string machineName, string domainName)
    {
        // Generate a stable agent ID based on machine name and domain
        var combined = $"{domainName}\\{machineName}".ToLowerInvariant();
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(combined))
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }
}

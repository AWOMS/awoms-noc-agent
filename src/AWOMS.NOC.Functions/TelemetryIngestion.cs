using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using Azure;
using AWOMS.NOC.Shared.Models;
using AWOMS.NOC.Shared;
using System.Text.Json;
using System.Net;
using System.Globalization;

namespace AWOMS.NOC.Functions;

public class TelemetryIngestion
{
    private readonly ILogger<TelemetryIngestion> _logger;
    private readonly TableServiceClient _tableServiceClient;

    private static readonly string[] TrendingMetricPrefixes =
    [
        "CPU|CPU Usage",
        "Memory|Memory Usage",
        "Memory|Available Memory",
        "Disk|Free Space (",
        "Disk|Used Space (",
        "Network|Interface Status",
        "NetworkConnectivity|Ping Status",
        "NetworkConnectivity|Ping Latency",
        "System|Uptime",
        "System|Pending Reboot",
        "EventLog|Error Count",
        "EventLog|Warning Count",
        "WindowsUpdate|Available Updates",
        "WindowsUpdate|Critical Updates Available",
        "ActiveDirectory|Users With Expired Passwords"
    ];

    public TelemetryIngestion(ILogger<TelemetryIngestion> logger, TableServiceClient tableServiceClient)
    {
        _logger = logger;
        _tableServiceClient = tableServiceClient;
    }

    [Function("TelemetryIngestion")]
    public async Task<TelemetryIngestionOutput> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "telemetry")] HttpRequestData req)
    {
        _logger.LogInformation("Telemetry ingestion function triggered");

        var output = new TelemetryIngestionOutput();

        try
        {
            // Validate API key
            if (!req.Headers.TryGetValues(Constants.ApiKeyHeaderName, out var apiKeyValues))
            {
                _logger.LogWarning("Missing API key header");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Missing API key");
                output.HttpResponse = unauthorizedResponse;
                return output;
            }

            var apiKey = apiKeyValues.FirstOrDefault();
            var expectedApiKey = Environment.GetEnvironmentVariable("ApiKey");
            
            if (string.IsNullOrEmpty(apiKey) || apiKey != expectedApiKey)
            {
                _logger.LogWarning("Invalid API key");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                await unauthorizedResponse.WriteStringAsync("Invalid API key");
                output.HttpResponse = unauthorizedResponse;
                return output;
            }

            // Parse request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
            var payload = JsonSerializer.Deserialize<TelemetryPayload>(requestBody);

            if (payload == null || string.IsNullOrEmpty(payload.AgentId))
            {
                _logger.LogWarning("Invalid payload");
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid payload");
                output.HttpResponse = badRequestResponse;
                return output;
            }

            _logger.LogInformation("Processing telemetry from {AgentId}", payload.AgentId);

            // Update or create machine entity
            await UpdateMachineEntity(payload);

            // Store current snapshot and historical trends
            await StoreTelemetryMetrics(payload);

            // Queue alerts
            output.AlertMessages = new List<string>();
            foreach (var alert in payload.Alerts)
            {
                var alertJson = JsonSerializer.Serialize(alert);
                output.AlertMessages.Add(alertJson);
                _logger.LogInformation("Queued alert: {AlertId}", alert.AlertId);
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteStringAsync("Telemetry received successfully");
            output.HttpResponse = response;
            return output;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing telemetry");
            var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
            await errorResponse.WriteStringAsync($"Error: {ex.Message}");
            output.HttpResponse = errorResponse;
            return output;
        }
    }

    private async Task UpdateMachineEntity(TelemetryPayload payload)
    {
        try
        {
            var machineTable = GetMachinesTable();
            var machineEntity = new TableMachineEntity
            {
                PartitionKey = "machines",
                RowKey = payload.AgentId,
                AgentId = payload.AgentId,
                MachineName = payload.MachineName,
                DomainName = payload.DomainName,
                IpAddress = payload.IpAddress,
                OsVersion = payload.OsVersion,
                LastHeartbeat = DateTime.UtcNow,
                IsOnline = true
            };

            await machineTable.UpsertEntityAsync(machineEntity, TableUpdateMode.Replace);
            _logger.LogInformation("Updated machine entity for {AgentId}", payload.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating machine entity");
        }
    }

    private async Task StoreTelemetryMetrics(TelemetryPayload payload)
    {
        try
        {
            var snapshotTask = UpsertMetricSnapshots(payload);
            var historyTask = InsertMetricHistory(payload);

            await Task.WhenAll(snapshotTask, historyTask);

            _logger.LogInformation(
                "Stored {SnapshotCount} snapshot metrics and {HistoryCount} historical metrics for {AgentId}",
                payload.Metrics.Count,
                payload.Metrics.Count(IsTrendingMetric),
                payload.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing telemetry metrics");
        }
    }

    private async Task UpsertMetricSnapshots(TelemetryPayload payload)
    {
        var snapshotTable = GetMetricSnapshotTable();
        var entities = payload.Metrics.Select(metric => new TableMetricSnapshotEntity
        {
            PartitionKey = payload.AgentId,
            RowKey = BuildSnapshotRowKey(metric),
            MachineName = payload.MachineName,
            Category = metric.Category,
            MetricName = metric.Name,
            MetricValue = ConvertMetricValue(metric.Value),
            Unit = metric.Unit,
            MetricTimestamp = metric.Timestamp
        }).ToList();

        await SubmitBatches(
            snapshotTable,
            entities,
            entity => new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity));
    }

    private async Task InsertMetricHistory(TelemetryPayload payload)
    {
        var historyTable = GetMetricHistoryTable();
        var entities = payload.Metrics
            .Where(IsTrendingMetric)
            .Select(metric => new TableMetricHistoryEntity
            {
                PartitionKey = payload.AgentId,
                RowKey = BuildHistoryRowKey(metric),
                MachineName = payload.MachineName,
                Category = metric.Category,
                MetricName = metric.Name,
                MetricValue = ConvertMetricValue(metric.Value),
                Unit = metric.Unit,
                MetricTimestamp = metric.Timestamp
            })
            .ToList();

        await SubmitBatches(
            historyTable,
            entities,
            entity => new TableTransactionAction(TableTransactionActionType.Add, entity));
    }

    private static async Task SubmitBatches<T>(
        TableClient tableClient,
        IReadOnlyCollection<T> entities,
        Func<T, TableTransactionAction> actionFactory)
        where T : class, ITableEntity, new()
    {
        if (entities.Count == 0)
        {
            return;
        }

        foreach (var batch in entities.Chunk(100))
        {
            var actions = batch.Select(actionFactory).ToList();
            await tableClient.SubmitTransactionAsync(actions);
        }
    }

    private static string BuildSnapshotRowKey(MetricData metric)
    {
        return $"{SanitizeForKey(metric.Category)}_{SanitizeForKey(metric.Name)}";
    }

    private static string BuildHistoryRowKey(MetricData metric)
    {
        var invertedTicks = DateTime.MaxValue.Ticks - metric.Timestamp.Ticks;
        return $"{invertedTicks:D19}_{SanitizeForKey(metric.Category)}_{SanitizeForKey(metric.Name)}";
    }

    private static string SanitizeForKey(string value)
    {
        return value
            .Replace("/", "-")
            .Replace("\\", "-")
            .Replace("#", "-")
            .Replace("?", "-");
    }

    private static bool IsTrendingMetric(MetricData metric)
    {
        var composite = $"{metric.Category}|{metric.Name}";
        return TrendingMetricPrefixes.Any(prefix => composite.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
    }

    private static string ConvertMetricValue(object? value)
    {
        return value switch
        {
            null => string.Empty,
            DateTime dateTime => dateTime.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            DateTimeOffset dateTimeOffset => dateTimeOffset.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture),
            JsonElement jsonElement => jsonElement.ToString(),
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
            _ => value.ToString() ?? string.Empty
        };
    }

    private TableClient GetMachinesTable()
    {
        return _tableServiceClient.GetTableClient(Constants.MachinesTableName);
    }

    private TableClient GetMetricSnapshotTable()
    {
        return _tableServiceClient.GetTableClient(Constants.MetricSnapshotTableName);
    }

    private TableClient GetMetricHistoryTable()
    {
        return _tableServiceClient.GetTableClient(Constants.MetricHistoryTableName);
    }
}

public class TelemetryIngestionOutput
{
    [QueueOutput(Constants.AlertsQueueName)]
    public List<string> AlertMessages { get; set; } = new();
    
    public HttpResponseData? HttpResponse { get; set; }
}

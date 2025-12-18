using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Azure.Data.Tables;
using AWOMS.NOC.Shared.Models;
using AWOMS.NOC.Shared;
using System.Text.Json;
using System.Net;

namespace AWOMS.NOC.Functions;

public class TelemetryIngestion
{
    private readonly ILogger<TelemetryIngestion> _logger;
    private readonly TableServiceClient _tableServiceClient;

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

            // Store metrics in telemetry table
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
            var machineTable = _tableServiceClient.GetTableClient(Constants.MachineTableName);
            await machineTable.CreateIfNotExistsAsync();

            var machineEntity = new MachineEntity
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

            await machineTable.UpsertEntityAsync(machineEntity);
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
            var telemetryTable = _tableServiceClient.GetTableClient(Constants.TelemetryTableName);
            await telemetryTable.CreateIfNotExistsAsync();

            foreach (var metric in payload.Metrics)
            {
                // Use inverted ticks for descending order (most recent first)
                var invertedTicks = DateTime.MaxValue.Ticks - metric.Timestamp.Ticks;
                var rowKey = $"{invertedTicks:D19}_{metric.Category}_{metric.Name}";

                var telemetryEntity = new TelemetryEntity
                {
                    PartitionKey = payload.AgentId,
                    RowKey = rowKey,
                    AgentId = payload.AgentId,
                    MachineName = payload.MachineName,
                    Category = metric.Category,
                    MetricName = metric.Name,
                    MetricValue = JsonSerializer.Serialize(metric.Value),
                    Unit = metric.Unit,
                    MetricTimestamp = metric.Timestamp
                };

                await telemetryTable.AddEntityAsync(telemetryEntity);
            }

            _logger.LogInformation("Stored {Count} metrics for {AgentId}", payload.Metrics.Count, payload.AgentId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error storing telemetry metrics");
        }
    }
}

public class TelemetryIngestionOutput
{
    [QueueOutput(Constants.AlertsQueueName)]
    public List<string> AlertMessages { get; set; } = new();
    
    public HttpResponseData? HttpResponse { get; set; }
}

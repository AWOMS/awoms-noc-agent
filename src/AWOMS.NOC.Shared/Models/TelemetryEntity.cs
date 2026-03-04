using System.Text.Json.Serialization;

namespace AWOMS.NOC.Shared.Models;

public class TelemetryEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
    
    // Telemetry Data
    public string AgentId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public object? MetricValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime MetricTimestamp { get; set; }
}

using Azure;
using Azure.Data.Tables;

namespace AWOMS.NOC.Shared.Models;

public class TelemetryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty; // AgentId
    public string RowKey { get; set; } = string.Empty; // InvertedTicks_Category_MetricName
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    // Telemetry Data
    public string AgentId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string MetricValue { get; set; } = string.Empty; // Stored as JSON string
    public string Unit { get; set; } = string.Empty;
    public DateTime MetricTimestamp { get; set; }
}

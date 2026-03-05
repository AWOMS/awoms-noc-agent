using Azure;
using Azure.Data.Tables;

namespace AWOMS.NOC.Shared.Models;

public class TableMetricHistoryEntity : ITableEntity
{
    public string PartitionKey { get; set; } = string.Empty;
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string MachineName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string MetricValue { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public DateTime MetricTimestamp { get; set; }
}

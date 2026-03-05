using Azure;
using Azure.Data.Tables;

namespace AWOMS.NOC.Shared.Models;

public class TableMachineEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "machines";
    public string RowKey { get; set; } = string.Empty;
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public string AgentId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public DateTime LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastAlertSent { get; set; }
}

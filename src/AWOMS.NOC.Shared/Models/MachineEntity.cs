using Azure;
using Azure.Data.Tables;

namespace AWOMS.NOC.Shared.Models;

public class MachineEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "machines";
    public string RowKey { get; set; } = string.Empty; // AgentId
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }
    
    // Machine Information
    public string AgentId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    
    // Status
    public DateTime LastHeartbeat { get; set; }
    public bool IsOnline { get; set; }
    public DateTime? LastAlertSent { get; set; }
}

using System.Text.Json.Serialization;

namespace AWOMS.NOC.Shared.Models;

public class MachineEntity
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("_etag")]
    public string? ETag { get; set; }
    
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

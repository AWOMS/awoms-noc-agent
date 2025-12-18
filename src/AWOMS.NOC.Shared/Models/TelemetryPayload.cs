namespace AWOMS.NOC.Shared.Models;

public class TelemetryPayload
{
    public string AgentId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string DomainName { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public string OsVersion { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public List<MetricData> Metrics { get; set; } = new();
    public List<AlertData> Alerts { get; set; } = new();
}

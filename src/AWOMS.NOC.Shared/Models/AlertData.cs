namespace AWOMS.NOC.Shared.Models;

public class AlertData
{
    public string AlertId { get; set; } = Guid.NewGuid().ToString();
    public string AgentId { get; set; } = string.Empty;
    public string MachineName { get; set; } = string.Empty;
    public string Severity { get; set; } = "Warning"; // Warning, Critical
    public string Category { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public object? CurrentValue { get; set; }
    public object? ThresholdValue { get; set; }
    public DateTime Timestamp { get; set; }
}

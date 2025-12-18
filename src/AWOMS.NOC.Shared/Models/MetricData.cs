namespace AWOMS.NOC.Shared.Models;

public class MetricData
{
    public string Category { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public object? Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}

namespace AWOMS.NOC.Agent;

public class ThresholdsConfiguration
{
    public double DiskSpaceCriticalPercent { get; set; } = 10.0;
    public double DiskSpaceWarningPercent { get; set; } = 20.0;
    public double MemoryUsageCriticalPercent { get; set; } = 90.0;
    public double MemoryUsageWarningPercent { get; set; } = 80.0;
    public double CpuUsageCriticalPercent { get; set; } = 95.0;
    public double CpuUsageWarningPercent { get; set; } = 85.0;
    public double DiskQueueCritical { get; set; } = 3.0;
    public int DiskQueueSustainedMinutes { get; set; } = 15;
    public int HeartbeatTimeoutMinutes { get; set; } = 5;
    public int WindowsUpdatePendingDays { get; set; } = 7;
}

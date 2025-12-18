namespace AWOMS.NOC.Shared;

public static class Constants
{
    // Table Storage Names
    public const string MachineTableName = "machines";
    public const string TelemetryTableName = "telemetry";
    
    // Queue Storage Names
    public const string AlertsQueueName = "alerts";
    
    // API Configuration
    public const string ApiKeyHeaderName = "x-api-key";
}

public static class Thresholds
{
    public const double DiskSpaceCriticalPercent = 10.0;
    public const double DiskSpaceWarningPercent = 20.0;
    public const double MemoryUsageCriticalPercent = 90.0;
    public const double MemoryUsageWarningPercent = 80.0;
    public const double CpuUsageCriticalPercent = 95.0;
    public const double CpuUsageWarningPercent = 85.0;
    public const double DiskQueueCritical = 3.0;
    public const int DiskQueueSustainedMinutes = 15;
    public const int HeartbeatTimeoutMinutes = 5;
    public const int WindowsUpdatePendingDays = 7;
}

namespace AWOMS.NOC.Shared;

public static class Constants
{
    // Azure Table Storage
    public const string MachinesTableName = "machines";
    public const string MetricSnapshotTableName = "machinemetrics";
    public const string MetricHistoryTableName = "metrichistory";
    
    // Queue Storage Names
    public const string AlertsQueueName = "alerts";
    
    // API Configuration
    public const string ApiKeyHeaderName = "x-api-key";
}

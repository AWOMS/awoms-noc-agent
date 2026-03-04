namespace AWOMS.NOC.Shared;

public static class Constants
{
    // Cosmos DB
    public const string CosmosDatabaseName = "awomsnoc";
    public const string MachineContainerName = "machines";
    public const string TelemetryContainerName = "telemetry";
    public const string AgentPartitionKeyPath = "/agentId";
    
    // Queue Storage Names
    public const string AlertsQueueName = "alerts";
    
    // API Configuration
    public const string ApiKeyHeaderName = "x-api-key";
}

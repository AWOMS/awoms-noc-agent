namespace AWOMS.NOC.Agent;

public class AgentConfiguration
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int CollectionIntervalSeconds { get; set; } = 60;
    public int ReportingIntervalSeconds { get; set; } = 300;
    public bool EnableLocalAlerts { get; set; } = true;
}

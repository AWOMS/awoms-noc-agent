namespace AWOMS.NOC.Agent;

public class AgentConfiguration
{
    public string ApiEndpoint { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public int CollectionIntervalSeconds { get; set; } = 60;
    public int ReportingIntervalSeconds { get; set; } = 300;
    public bool EnableLocalAlerts { get; set; } = true;
    public string PublicIpUrl { get; set; } = "https://api.ipify.org/";
    public string[] PingTargets { get; set; } = ["8.8.8.8", "1.1.1.1"];
    public string[] MonitoredServices { get; set; } =
    [
        "Dnscache",
        "LanmanServer",
        "LanmanWorkstation",
        "Spooler",
        "W32Time",
        "WinDefend"
    ];
}

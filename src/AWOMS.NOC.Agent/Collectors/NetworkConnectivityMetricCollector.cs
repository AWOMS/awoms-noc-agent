using AWOMS.NOC.Shared.Models;
using System.Net.NetworkInformation;

namespace AWOMS.NOC.Agent.Collectors;

public class NetworkConnectivityMetricCollector : IMetricCollector
{
    private readonly AgentConfiguration _configuration;

    public NetworkConnectivityMetricCollector(AgentConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();

        foreach (var target in _configuration.PingTargets.Where(t => !string.IsNullOrWhiteSpace(t)).Distinct())
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(target, 3000);

                var isSuccess = reply.Status == IPStatus.Success;
                metrics.Add(new MetricData
                {
                    Category = "NetworkConnectivity",
                    Name = $"Ping Status ({target})",
                    Value = isSuccess ? "Success" : reply.Status.ToString(),
                    Unit = "status",
                    Timestamp = DateTime.UtcNow
                });

                metrics.Add(new MetricData
                {
                    Category = "NetworkConnectivity",
                    Name = $"Ping Latency ({target})",
                    Value = isSuccess ? reply.RoundtripTime : -1,
                    Unit = "ms",
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                metrics.Add(new MetricData
                {
                    Category = "NetworkConnectivity",
                    Name = $"Ping Status ({target})",
                    Value = $"Error: {ex.Message}",
                    Unit = "error",
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        return metrics;
    }
}
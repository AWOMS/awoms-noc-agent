using AWOMS.NOC.Shared.Models;

namespace AWOMS.NOC.Agent.Collectors;

public class PublicIpMetricCollector : IMetricCollector
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly AgentConfiguration _configuration;

    public PublicIpMetricCollector(IHttpClientFactory httpClientFactory, AgentConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
    }

    public async Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();

        try
        {
            var client = _httpClientFactory.CreateClient();
            var publicIp = (await client.GetStringAsync(_configuration.PublicIpUrl)).Trim();

            metrics.Add(new MetricData
            {
                Category = "Network",
                Name = "Public IP Address",
                Value = publicIp,
                Unit = "ip",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "Network",
                Name = "Public IP Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }

        return metrics;
    }
}
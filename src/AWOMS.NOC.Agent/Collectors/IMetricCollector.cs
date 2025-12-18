using AWOMS.NOC.Shared.Models;

namespace AWOMS.NOC.Agent.Collectors;

public interface IMetricCollector
{
    Task<List<MetricData>> CollectAsync();
}

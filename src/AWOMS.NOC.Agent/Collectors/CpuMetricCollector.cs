using AWOMS.NOC.Shared.Models;
using System.Diagnostics;

namespace AWOMS.NOC.Agent.Collectors;

public class CpuMetricCollector : IMetricCollector
{
    private PerformanceCounter? _cpuCounter;

    public CpuMetricCollector()
    {
        try
        {
            // Note: PerformanceCounter requires elevated privileges on Windows
            _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            _cpuCounter.NextValue(); // First call always returns 0, prime the counter
        }
        catch (Exception)
        {
            // Gracefully handle if performance counters are not available
            _cpuCounter = null;
        }
    }

    public async Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            if (_cpuCounter != null)
            {
                await Task.Delay(100); // Small delay for accurate reading
                var cpuUsage = _cpuCounter.NextValue();
                
                metrics.Add(new MetricData
                {
                    Category = "CPU",
                    Name = "CPU Usage",
                    Value = Math.Round(cpuUsage, 2),
                    Unit = "%",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "CPU",
                Name = "CPU Usage Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return metrics;
    }
}

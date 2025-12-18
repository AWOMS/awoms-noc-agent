using AWOMS.NOC.Shared.Models;
using System.Diagnostics;

namespace AWOMS.NOC.Agent.Collectors;

public class MemoryMetricCollector : IMetricCollector
{
    private PerformanceCounter? _availableMemoryCounter;

    public MemoryMetricCollector()
    {
        try
        {
            _availableMemoryCounter = new PerformanceCounter("Memory", "Available MBytes");
        }
        catch (Exception)
        {
            _availableMemoryCounter = null;
        }
    }

    public async Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            // Get total physical memory using GC (cross-platform fallback)
            var gcMemoryInfo = GC.GetGCMemoryInfo();
            var totalMemoryMB = gcMemoryInfo.TotalAvailableMemoryBytes / (1024.0 * 1024.0);
            
            double availableMemoryMB = 0;
            if (_availableMemoryCounter != null)
            {
                availableMemoryMB = _availableMemoryCounter.NextValue();
            }
            else
            {
                // Fallback: use GC information
                availableMemoryMB = (gcMemoryInfo.TotalAvailableMemoryBytes - gcMemoryInfo.MemoryLoadBytes) / (1024.0 * 1024.0);
            }
            
            var usedMemoryMB = totalMemoryMB - availableMemoryMB;
            var memoryUsagePercent = (usedMemoryMB / totalMemoryMB) * 100;
            
            metrics.Add(new MetricData
            {
                Category = "Memory",
                Name = "Memory Usage",
                Value = Math.Round(memoryUsagePercent, 2),
                Unit = "%",
                Timestamp = DateTime.UtcNow
            });
            
            metrics.Add(new MetricData
            {
                Category = "Memory",
                Name = "Available Memory",
                Value = Math.Round(availableMemoryMB, 2),
                Unit = "MB",
                Timestamp = DateTime.UtcNow
            });
            
            metrics.Add(new MetricData
            {
                Category = "Memory",
                Name = "Total Memory",
                Value = Math.Round(totalMemoryMB, 2),
                Unit = "MB",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "Memory",
                Name = "Memory Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return await Task.FromResult(metrics);
    }
}

using AWOMS.NOC.Shared.Models;
using System.ServiceProcess;

namespace AWOMS.NOC.Agent.Collectors;

public class ServiceMetricCollector : IMetricCollector
{
    // Critical services to monitor - can be configured via appsettings
    private readonly string[] _criticalServices = new[]
    {
        "Dnscache",        // DNS Client
        "LanmanServer",    // Server (file sharing)
        "LanmanWorkstation", // Workstation
        "Spooler",         // Print Spooler
        "W32Time",         // Windows Time
        "WinDefend"        // Windows Defender
    };

    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            var services = ServiceController.GetServices();
            
            foreach (var serviceName in _criticalServices)
            {
                try
                {
                    var service = services.FirstOrDefault(s => 
                        s.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase));
                    
                    if (service != null)
                    {
                        metrics.Add(new MetricData
                        {
                            Category = "Service",
                            Name = $"Service Status ({service.DisplayName})",
                            Value = service.Status.ToString(),
                            Unit = "status",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    else
                    {
                        metrics.Add(new MetricData
                        {
                            Category = "Service",
                            Name = $"Service Status ({serviceName})",
                            Value = "Not Found",
                            Unit = "status",
                            Timestamp = DateTime.UtcNow
                        });
                    }
                }
                catch (Exception ex)
                {
                    metrics.Add(new MetricData
                    {
                        Category = "Service",
                        Name = $"Service Status ({serviceName})",
                        Value = $"Error: {ex.Message}",
                        Unit = "error",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "Service",
                Name = "Service Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return Task.FromResult(metrics);
    }
}

using AWOMS.NOC.Shared.Models;
using System.Management;

namespace AWOMS.NOC.Agent.Collectors;

public class SecurityMetricCollector : IMetricCollector
{
    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            // Check Windows Defender status using WMI
            var avStatus = CheckAntivirusStatus();
            metrics.Add(new MetricData
            {
                Category = "Security",
                Name = "Antivirus Status",
                Value = avStatus,
                Unit = "status",
                Timestamp = DateTime.UtcNow
            });
            
            // Check Windows Firewall status
            var firewallStatus = CheckFirewallStatus();
            metrics.Add(new MetricData
            {
                Category = "Security",
                Name = "Firewall Status",
                Value = firewallStatus,
                Unit = "status",
                Timestamp = DateTime.UtcNow
            });
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "Security",
                Name = "Security Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return Task.FromResult(metrics);
    }

    private string CheckAntivirusStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM AntiVirusProduct");
            var results = searcher.Get();
            
            if (results.Count == 0)
            {
                return "No antivirus detected";
            }
            
            foreach (ManagementObject queryObj in results)
            {
                var displayName = queryObj["displayName"]?.ToString() ?? "Unknown";
                var productState = queryObj["productState"];
                
                if (productState != null)
                {
                    var state = Convert.ToInt32(productState);
                    // Bit 13 indicates if real-time protection is enabled
                    var isEnabled = (state & 0x1000) != 0;
                    // Bit 8-11 indicates if definitions are up to date
                    var isUpdated = (state & 0x0010) == 0;
                    
                    if (!isEnabled)
                    {
                        return $"{displayName}: Disabled";
                    }
                    if (!isUpdated)
                    {
                        return $"{displayName}: Outdated";
                    }
                    return $"{displayName}: Active and updated";
                }
            }
            
            return "Status unknown";
        }
        catch (Exception ex)
        {
            return $"Error checking antivirus: {ex.Message}";
        }
    }

    private string CheckFirewallStatus()
    {
        try
        {
            using var searcher = new ManagementObjectSearcher(@"root\SecurityCenter2", "SELECT * FROM FirewallProduct");
            var results = searcher.Get();
            
            if (results.Count == 0)
            {
                return "No firewall detected";
            }
            
            foreach (ManagementObject queryObj in results)
            {
                var displayName = queryObj["displayName"]?.ToString() ?? "Unknown";
                var productState = queryObj["productState"];
                
                if (productState != null)
                {
                    var state = Convert.ToInt32(productState);
                    var isEnabled = (state & 0x1000) != 0;
                    
                    if (!isEnabled)
                    {
                        return $"{displayName}: Disabled";
                    }
                    return $"{displayName}: Enabled";
                }
            }
            
            return "Status unknown";
        }
        catch (Exception ex)
        {
            return $"Error checking firewall: {ex.Message}";
        }
    }
}

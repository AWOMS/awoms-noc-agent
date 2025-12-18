using AWOMS.NOC.Shared.Models;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace AWOMS.NOC.Agent.Collectors;

public class SystemMetricCollector : IMetricCollector
{
    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            // Last Boot Time
            var lastBootTime = DateTime.UtcNow - TimeSpan.FromMilliseconds(Environment.TickCount64);
            metrics.Add(new MetricData
            {
                Category = "System",
                Name = "Last Boot Time",
                Value = lastBootTime.ToString("yyyy-MM-dd HH:mm:ss"),
                Unit = "datetime",
                Timestamp = DateTime.UtcNow
            });
            
            metrics.Add(new MetricData
            {
                Category = "System",
                Name = "Uptime",
                Value = Math.Round(TimeSpan.FromMilliseconds(Environment.TickCount64).TotalHours, 2),
                Unit = "hours",
                Timestamp = DateTime.UtcNow
            });
            
            // Pending Reboot Detection (Windows Registry)
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var pendingReboot = CheckPendingReboot();
                metrics.Add(new MetricData
                {
                    Category = "System",
                    Name = "Pending Reboot",
                    Value = pendingReboot,
                    Unit = "boolean",
                    Timestamp = DateTime.UtcNow
                });
                
                // Windows Update Status
                var updateStatus = CheckWindowsUpdateStatus();
                metrics.Add(new MetricData
                {
                    Category = "System",
                    Name = "Windows Update Status",
                    Value = updateStatus,
                    Unit = "status",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "System",
                Name = "System Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return Task.FromResult(metrics);
    }

    private bool CheckPendingReboot()
    {
        try
        {
            // Check Component Based Servicing
            using var cbsKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Component Based Servicing\RebootPending");
            if (cbsKey != null) return true;
            
            // Check Windows Update
            using var wuKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\RebootRequired");
            if (wuKey != null) return true;
            
            // Check pending file rename operations
            using var sessionKey = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Control\Session Manager");
            var pendingFileRenameOperations = sessionKey?.GetValue("PendingFileRenameOperations");
            if (pendingFileRenameOperations != null) return true;
            
            return false;
        }
        catch
        {
            return false;
        }
    }

    private string CheckWindowsUpdateStatus()
    {
        try
        {
            // This is a simplified check - in production, you might want to use Windows Update Agent API
            using var wuKey = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\WindowsUpdate\Auto Update\Results\Install");
            if (wuKey != null)
            {
                var lastSuccessTime = wuKey.GetValue("LastSuccessTime") as string;
                if (!string.IsNullOrEmpty(lastSuccessTime) && DateTime.TryParse(lastSuccessTime, out var lastUpdate))
                {
                    var daysSinceUpdate = (DateTime.UtcNow - lastUpdate).TotalDays;
                    if (daysSinceUpdate > 7)
                    {
                        return $"Updates pending ({Math.Round(daysSinceUpdate)} days since last update)";
                    }
                    return $"Up to date (last update: {Math.Round(daysSinceUpdate)} days ago)";
                }
            }
            return "Status unknown";
        }
        catch
        {
            return "Unable to check";
        }
    }
}

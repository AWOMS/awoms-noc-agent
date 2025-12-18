using AWOMS.NOC.Shared.Models;
using AWOMS.NOC.Shared;

namespace AWOMS.NOC.Agent.Services;

public class AlertEvaluator
{
    private readonly ThresholdsConfiguration _thresholds;

    public AlertEvaluator(ThresholdsConfiguration thresholds)
    {
        _thresholds = thresholds;
    }

    public List<AlertData> EvaluateMetrics(List<MetricData> metrics, string agentId, string machineName)
    {
        var alerts = new List<AlertData>();

        foreach (var metric in metrics)
        {
            AlertData? alert = null;

            switch (metric.Category)
            {
                case "Disk":
                    if (metric.Name.Contains("Free Space") && metric.Value is double freeSpace)
                    {
                        if (freeSpace < _thresholds.DiskSpaceCriticalPercent)
                        {
                            alert = CreateAlert(agentId, machineName, "Critical", metric, 
                                $"Disk free space critically low: {freeSpace:F2}%", 
                                _thresholds.DiskSpaceCriticalPercent);
                        }
                        else if (freeSpace < _thresholds.DiskSpaceWarningPercent)
                        {
                            alert = CreateAlert(agentId, machineName, "Warning", metric, 
                                $"Disk free space low: {freeSpace:F2}%", 
                                _thresholds.DiskSpaceWarningPercent);
                        }
                    }
                    break;

                case "Memory":
                    if (metric.Name == "Memory Usage" && metric.Value is double memUsage)
                    {
                        if (memUsage > _thresholds.MemoryUsageCriticalPercent)
                        {
                            alert = CreateAlert(agentId, machineName, "Critical", metric, 
                                $"Memory usage critically high: {memUsage:F2}%", 
                                _thresholds.MemoryUsageCriticalPercent);
                        }
                        else if (memUsage > _thresholds.MemoryUsageWarningPercent)
                        {
                            alert = CreateAlert(agentId, machineName, "Warning", metric, 
                                $"Memory usage high: {memUsage:F2}%", 
                                _thresholds.MemoryUsageWarningPercent);
                        }
                    }
                    break;

                case "CPU":
                    if (metric.Name == "CPU Usage" && metric.Value is double cpuUsage)
                    {
                        if (cpuUsage > _thresholds.CpuUsageCriticalPercent)
                        {
                            alert = CreateAlert(agentId, machineName, "Critical", metric, 
                                $"CPU usage critically high: {cpuUsage:F2}%", 
                                _thresholds.CpuUsageCriticalPercent);
                        }
                        else if (cpuUsage > _thresholds.CpuUsageWarningPercent)
                        {
                            alert = CreateAlert(agentId, machineName, "Warning", metric, 
                                $"CPU usage high: {cpuUsage:F2}%", 
                                _thresholds.CpuUsageWarningPercent);
                        }
                    }
                    break;

                case "System":
                    if (metric.Name == "Pending Reboot" && metric.Value is bool pendingReboot && pendingReboot)
                    {
                        alert = CreateAlert(agentId, machineName, "Warning", metric, 
                            "System has pending reboot", null);
                    }
                    else if (metric.Name == "Windows Update Status" && metric.Value is string updateStatus 
                        && updateStatus.Contains("pending", StringComparison.OrdinalIgnoreCase))
                    {
                        alert = CreateAlert(agentId, machineName, "Warning", metric, 
                            $"Windows updates pending: {updateStatus}", _thresholds.WindowsUpdatePendingDays);
                    }
                    break;

                case "Security":
                    if (metric.Name == "Antivirus Status" && metric.Value is string avStatus)
                    {
                        if (avStatus.Contains("Disabled", StringComparison.OrdinalIgnoreCase) || 
                            avStatus.Contains("Outdated", StringComparison.OrdinalIgnoreCase))
                        {
                            alert = CreateAlert(agentId, machineName, "Critical", metric, 
                                $"Antivirus issue detected: {avStatus}", null);
                        }
                    }
                    break;

                case "Service":
                    if (metric.Name.Contains("Service Status") && metric.Value is string serviceStatus 
                        && serviceStatus.Equals("Stopped", StringComparison.OrdinalIgnoreCase))
                    {
                        alert = CreateAlert(agentId, machineName, "Critical", metric, 
                            $"Critical service stopped: {metric.Name}", null);
                    }
                    break;

                case "EventLog":
                    if (metric.Name.Contains("Critical Event"))
                    {
                        alert = CreateAlert(agentId, machineName, "Warning", metric, 
                            $"Critical event detected: {metric.Value}", null);
                    }
                    break;
            }

            if (alert != null)
            {
                alerts.Add(alert);
            }
        }

        return alerts;
    }

    private AlertData CreateAlert(string agentId, string machineName, string severity, 
        MetricData metric, string message, object? thresholdValue)
    {
        return new AlertData
        {
            AgentId = agentId,
            MachineName = machineName,
            Severity = severity,
            Category = metric.Category,
            MetricName = metric.Name,
            Message = message,
            CurrentValue = metric.Value,
            ThresholdValue = thresholdValue,
            Timestamp = DateTime.UtcNow
        };
    }
}

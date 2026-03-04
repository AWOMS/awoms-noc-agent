using AWOMS.NOC.Shared.Models;
using System.Runtime.InteropServices;

namespace AWOMS.NOC.Agent.Collectors;

public class WindowsUpdateMetricCollector : IMetricCollector
{
    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Task.FromResult(metrics);
        }

        try
        {
            var updateSessionType = Type.GetTypeFromProgID("Microsoft.Update.Session");
            if (updateSessionType == null)
            {
                return Task.FromResult(metrics);
            }

            dynamic updateSession = Activator.CreateInstance(updateSessionType)!;
            dynamic updateSearcher = updateSession.CreateUpdateSearcher();
            dynamic searchResult = updateSearcher.Search("IsInstalled=0 and IsHidden=0");
            int updateCount = (int)searchResult.Updates.Count;

            var criticalCount = 0;

            metrics.Add(new MetricData
            {
                Category = "WindowsUpdate",
                Name = "Available Updates",
                Value = updateCount,
                Unit = "count",
                Timestamp = DateTime.UtcNow
            });

            for (var i = 0; i < updateCount; i++)
            {
                dynamic update = searchResult.Updates[i];
                var severity = ((string?)update.MsrcSeverity) ?? "Unknown";
                var title = ((string?)update.Title) ?? $"Update {i + 1}";

                if (severity.Equals("Critical", StringComparison.OrdinalIgnoreCase) ||
                    severity.Equals("Important", StringComparison.OrdinalIgnoreCase))
                {
                    criticalCount++;
                }

                if (i < 10)
                {
                    metrics.Add(new MetricData
                    {
                        Category = "WindowsUpdate",
                        Name = $"Update Detail ({i + 1})",
                        Value = $"{severity}: {title}",
                        Unit = "status",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }

            metrics.Add(new MetricData
            {
                Category = "WindowsUpdate",
                Name = "Critical Updates Available",
                Value = criticalCount,
                Unit = "count",
                Timestamp = DateTime.UtcNow
            });

            dynamic historySearcher = updateSession.CreateUpdateSearcher();
            int historyCount = (int)historySearcher.GetTotalHistoryCount();
            if (historyCount > 0)
            {
                dynamic historyItems = historySearcher.QueryHistory(0, 1);
                if ((int)historyItems.Count > 0)
                {
                    dynamic latest = historyItems[0];
                    var installedDateUtc = ((DateTime)latest.Date).ToUniversalTime();
                    var daysSinceLastUpdate = Math.Round((DateTime.UtcNow - installedDateUtc).TotalDays, 1);

                    metrics.Add(new MetricData
                    {
                        Category = "WindowsUpdate",
                        Name = "Last Update Installed",
                        Value = installedDateUtc.ToString("yyyy-MM-dd HH:mm:ss"),
                        Unit = "datetime",
                        Timestamp = DateTime.UtcNow
                    });

                    metrics.Add(new MetricData
                    {
                        Category = "WindowsUpdate",
                        Name = "Days Since Last Update",
                        Value = daysSinceLastUpdate,
                        Unit = "days",
                        Timestamp = DateTime.UtcNow
                    });
                }
            }
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "WindowsUpdate",
                Name = "Windows Update Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }

        return Task.FromResult(metrics);
    }
}
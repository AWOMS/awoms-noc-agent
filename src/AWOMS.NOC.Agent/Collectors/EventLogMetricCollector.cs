using AWOMS.NOC.Shared.Models;
using System.Diagnostics;

namespace AWOMS.NOC.Agent.Collectors;

public class EventLogMetricCollector : IMetricCollector
{
    private DateTime _lastCheckTime = DateTime.UtcNow.AddHours(-1);

    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            // Check System event log for critical errors
            var criticalEvents = GetCriticalEvents("System");
            var applicationEvents = GetCriticalEvents("Application");
            
            metrics.Add(new MetricData
            {
                Category = "EventLog",
                Name = "System Critical Events",
                Value = criticalEvents.Count,
                Unit = "count",
                Timestamp = DateTime.UtcNow
            });
            
            metrics.Add(new MetricData
            {
                Category = "EventLog",
                Name = "Application Critical Events",
                Value = applicationEvents.Count,
                Unit = "count",
                Timestamp = DateTime.UtcNow
            });
            
            // Add details of critical events (limit to 5 most recent)
            var allCriticalEvents = criticalEvents.Concat(applicationEvents)
                .OrderByDescending(e => e.TimeGenerated)
                .Take(5);
            
            foreach (var evt in allCriticalEvents)
            {
                metrics.Add(new MetricData
                {
                    Category = "EventLog",
                    Name = $"Critical Event ({evt.Source})",
                    Value = $"EventID: {evt.InstanceId}, Message: {(evt.Message != null ? evt.Message.Substring(0, Math.Min(200, evt.Message.Length)) : string.Empty)}",
                    Unit = "event",
                    Timestamp = evt.TimeGenerated
                });
            }
            
            _lastCheckTime = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "EventLog",
                Name = "EventLog Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return Task.FromResult(metrics);
    }

    private List<EventLogEntry> GetCriticalEvents(string logName)
    {
        var criticalEvents = new List<EventLogEntry>();
        
        try
        {
            using var eventLog = new EventLog(logName);
            
            foreach (EventLogEntry entry in eventLog.Entries)
            {
                if (entry.TimeGenerated > _lastCheckTime &&
                    (entry.EntryType == EventLogEntryType.Error || entry.EntryType == EventLogEntryType.FailureAudit))
                {
                    criticalEvents.Add(entry);
                }
            }
        }
        catch (Exception)
        {
            // Silently fail if we can't access the event log
        }
        
        return criticalEvents;
    }
}

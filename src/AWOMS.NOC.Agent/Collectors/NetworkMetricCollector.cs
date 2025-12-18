using AWOMS.NOC.Shared.Models;
using System.Net.NetworkInformation;

namespace AWOMS.NOC.Agent.Collectors;

public class NetworkMetricCollector : IMetricCollector
{
    private Dictionary<string, long> _previousBytesSent = new();
    private Dictionary<string, long> _previousBytesReceived = new();
    private DateTime _previousCollectionTime = DateTime.UtcNow;

    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        var currentTime = DateTime.UtcNow;
        var timeDiff = (currentTime - _previousCollectionTime).TotalSeconds;
        
        try
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.OperationalStatus == OperationalStatus.Up 
                    && ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            
            foreach (var netInterface in interfaces)
            {
                var stats = netInterface.GetIPv4Statistics();
                
                metrics.Add(new MetricData
                {
                    Category = "Network",
                    Name = $"Interface Status ({netInterface.Name})",
                    Value = netInterface.OperationalStatus.ToString(),
                    Unit = "status",
                    Timestamp = DateTime.UtcNow
                });
                
                // Calculate bytes per second if we have previous data
                if (_previousBytesSent.ContainsKey(netInterface.Name) && timeDiff > 0)
                {
                    var bytesSentPerSec = (stats.BytesSent - _previousBytesSent[netInterface.Name]) / timeDiff;
                    var bytesReceivedPerSec = (stats.BytesReceived - _previousBytesReceived[netInterface.Name]) / timeDiff;
                    
                    metrics.Add(new MetricData
                    {
                        Category = "Network",
                        Name = $"Bytes Sent/Sec ({netInterface.Name})",
                        Value = Math.Round(bytesSentPerSec, 2),
                        Unit = "bytes/sec",
                        Timestamp = DateTime.UtcNow
                    });
                    
                    metrics.Add(new MetricData
                    {
                        Category = "Network",
                        Name = $"Bytes Received/Sec ({netInterface.Name})",
                        Value = Math.Round(bytesReceivedPerSec, 2),
                        Unit = "bytes/sec",
                        Timestamp = DateTime.UtcNow
                    });
                }
                
                // Update previous values
                _previousBytesSent[netInterface.Name] = stats.BytesSent;
                _previousBytesReceived[netInterface.Name] = stats.BytesReceived;
            }
            
            _previousCollectionTime = currentTime;
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "Network",
                Name = "Network Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return Task.FromResult(metrics);
    }
}

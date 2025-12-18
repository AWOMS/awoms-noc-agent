using AWOMS.NOC.Shared.Models;

namespace AWOMS.NOC.Agent.Collectors;

public class DiskMetricCollector : IMetricCollector
{
    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();
        
        try
        {
            var drives = DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed);
            
            foreach (var drive in drives)
            {
                var freeSpacePercent = (double)drive.AvailableFreeSpace / drive.TotalSize * 100;
                var usedSpacePercent = 100 - freeSpacePercent;
                
                metrics.Add(new MetricData
                {
                    Category = "Disk",
                    Name = $"Free Space ({drive.Name})",
                    Value = Math.Round(freeSpacePercent, 2),
                    Unit = "%",
                    Timestamp = DateTime.UtcNow
                });
                
                metrics.Add(new MetricData
                {
                    Category = "Disk",
                    Name = $"Used Space ({drive.Name})",
                    Value = Math.Round(usedSpacePercent, 2),
                    Unit = "%",
                    Timestamp = DateTime.UtcNow
                });
                
                metrics.Add(new MetricData
                {
                    Category = "Disk",
                    Name = $"Total Size ({drive.Name})",
                    Value = drive.TotalSize,
                    Unit = "bytes",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "Disk",
                Name = "Disk Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }
        
        return Task.FromResult(metrics);
    }
}

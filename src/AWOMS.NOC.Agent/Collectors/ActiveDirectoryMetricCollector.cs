using AWOMS.NOC.Shared.Models;
using System.DirectoryServices.AccountManagement;

namespace AWOMS.NOC.Agent.Collectors;

public class ActiveDirectoryMetricCollector : IMetricCollector
{
    private readonly ThresholdsConfiguration _thresholds;

    public ActiveDirectoryMetricCollector(ThresholdsConfiguration thresholds)
    {
        _thresholds = thresholds;
    }

    public Task<List<MetricData>> CollectAsync()
    {
        var metrics = new List<MetricData>();

        try
        {
            using var context = new PrincipalContext(ContextType.Domain);
            using var userPrincipal = new UserPrincipal(context);
            using var searcher = new PrincipalSearcher(userPrincipal);

            var users = searcher.FindAll();
            var staleUsers = new List<(string Name, double AgeDays)>();

            foreach (var principal in users)
            {
                if (principal is not UserPrincipal user || !user.Enabled.GetValueOrDefault())
                {
                    continue;
                }

                if (user.LastPasswordSet is not DateTime lastPasswordSet)
                {
                    continue;
                }

                var ageDays = (DateTime.UtcNow - lastPasswordSet.ToUniversalTime()).TotalDays;
                if (ageDays > _thresholds.PasswordMaxAgeDays)
                {
                    staleUsers.Add((user.SamAccountName ?? user.Name ?? "Unknown", ageDays));
                }
            }

            metrics.Add(new MetricData
            {
                Category = "ActiveDirectory",
                Name = "Users With Expired Passwords",
                Value = staleUsers.Count,
                Unit = "count",
                Timestamp = DateTime.UtcNow
            });

            foreach (var staleUser in staleUsers.Take(20))
            {
                metrics.Add(new MetricData
                {
                    Category = "ActiveDirectory",
                    Name = $"Password Age ({staleUser.Name})",
                    Value = Math.Round(staleUser.AgeDays, 1),
                    Unit = "days",
                    Timestamp = DateTime.UtcNow
                });
            }
        }
        catch (PrincipalServerDownException)
        {
            return Task.FromResult(metrics);
        }
        catch (Exception ex)
        {
            metrics.Add(new MetricData
            {
                Category = "ActiveDirectory",
                Name = "Active Directory Collection Error",
                Value = ex.Message,
                Unit = "error",
                Timestamp = DateTime.UtcNow
            });
        }

        return Task.FromResult(metrics);
    }
}
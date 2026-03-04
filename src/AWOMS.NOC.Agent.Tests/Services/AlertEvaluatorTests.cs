using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Services;
using AWOMS.NOC.Shared.Models;
using FluentAssertions;

namespace AWOMS.NOC.Agent.Tests.Services;

public class AlertEvaluatorTests
{
    private readonly AlertEvaluator _evaluator;

    public AlertEvaluatorTests()
    {
        _evaluator = new AlertEvaluator(new ThresholdsConfiguration
        {
            DiskSpaceCriticalPercent = 10,
            MemoryUsageCriticalPercent = 90,
            CpuUsageCriticalPercent = 95,
            PasswordMaxAgeDays = 31,
            PingLatencyWarningMs = 200,
            CriticalUpdatesPendingDays = 7
        });
    }

    [Fact]
    public void EvaluateMetrics_DiskCritical_ShouldCreateCriticalAlert()
    {
        var metrics = new List<MetricData>
        {
            new()
            {
                Category = "Disk",
                Name = "Free Space (C:)",
                Value = 5d,
                Unit = "%",
                Timestamp = DateTime.UtcNow
            }
        };

        var alerts = _evaluator.EvaluateMetrics(metrics, "agent-1", "machine-1");

        alerts.Should().ContainSingle();
        alerts[0].Severity.Should().Be("Critical");
        alerts[0].Category.Should().Be("Disk");
    }

    [Fact]
    public void EvaluateMetrics_ActiveDirectoryExpiredUsers_ShouldCreateWarningAlert()
    {
        var metrics = new List<MetricData>
        {
            new()
            {
                Category = "ActiveDirectory",
                Name = "Users With Expired Passwords",
                Value = 3,
                Unit = "count",
                Timestamp = DateTime.UtcNow
            }
        };

        var alerts = _evaluator.EvaluateMetrics(metrics, "agent-1", "machine-1");

        alerts.Should().ContainSingle();
        alerts[0].Severity.Should().Be("Warning");
        alerts[0].Category.Should().Be("ActiveDirectory");
    }

    [Fact]
    public void EvaluateMetrics_NetworkConnectivityFailure_ShouldCreateCriticalAlert()
    {
        var metrics = new List<MetricData>
        {
            new()
            {
                Category = "NetworkConnectivity",
                Name = "Ping Status (8.8.8.8)",
                Value = "TimedOut",
                Unit = "status",
                Timestamp = DateTime.UtcNow
            }
        };

        var alerts = _evaluator.EvaluateMetrics(metrics, "agent-1", "machine-1");

        alerts.Should().ContainSingle();
        alerts[0].Severity.Should().Be("Critical");
        alerts[0].Category.Should().Be("NetworkConnectivity");
    }

    [Fact]
    public void EvaluateMetrics_PingLatencyAboveThreshold_ShouldCreateWarningAlert()
    {
        var metrics = new List<MetricData>
        {
            new()
            {
                Category = "NetworkConnectivity",
                Name = "Ping Latency (8.8.8.8)",
                Value = 350,
                Unit = "ms",
                Timestamp = DateTime.UtcNow
            }
        };

        var alerts = _evaluator.EvaluateMetrics(metrics, "agent-1", "machine-1");

        alerts.Should().ContainSingle();
        alerts[0].Severity.Should().Be("Warning");
        alerts[0].Category.Should().Be("NetworkConnectivity");
    }

    [Fact]
    public void EvaluateMetrics_StaleWindowsUpdate_ShouldCreateCriticalAlert()
    {
        var metrics = new List<MetricData>
        {
            new()
            {
                Category = "WindowsUpdate",
                Name = "Days Since Last Update",
                Value = 12d,
                Unit = "days",
                Timestamp = DateTime.UtcNow
            }
        };

        var alerts = _evaluator.EvaluateMetrics(metrics, "agent-1", "machine-1");

        alerts.Should().ContainSingle();
        alerts[0].Severity.Should().Be("Critical");
        alerts[0].Category.Should().Be("WindowsUpdate");
    }
}
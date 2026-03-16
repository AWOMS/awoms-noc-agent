using AWOMS.NOC.Agent;
using FluentAssertions;

namespace AWOMS.NOC.Agent.Tests;

public class ConfigurationDefaultsTests
{
    [Fact]
    public void AgentConfiguration_Defaults_ShouldMatchExpectedSafeValues()
    {
        var config = new AgentConfiguration();

        config.CollectionIntervalSeconds.Should().Be(60);
        config.ReportingIntervalSeconds.Should().Be(300);
        config.EnableLocalAlerts.Should().BeTrue();
        config.PublicIpUrl.Should().Be("https://api.ipify.org/");
        config.PingTargets.Should().Contain(["8.8.8.8", "1.1.1.1"]);
        config.MonitoredServices.Should().NotBeEmpty();
    }

    [Fact]
    public void ThresholdsConfiguration_Defaults_ShouldMatchExpectedValues()
    {
        var thresholds = new ThresholdsConfiguration();

        thresholds.DiskSpaceCriticalPercent.Should().Be(10.0);
        thresholds.MemoryUsageCriticalPercent.Should().Be(90.0);
        thresholds.CpuUsageCriticalPercent.Should().Be(95.0);
        thresholds.PasswordMaxAgeDays.Should().Be(31);
        thresholds.PingLatencyWarningMs.Should().Be(200);
        thresholds.CriticalUpdatesPendingDays.Should().Be(7);
    }
}
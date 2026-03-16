using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Collectors;
using FluentAssertions;

namespace AWOMS.NOC.Agent.Tests.Collectors;

public class EnvironmentResilienceCollectorTests
{
    [Fact]
    public async Task ActiveDirectoryCollector_CollectAsync_ShouldNotThrowAcrossEnvironments()
    {
        var collector = new ActiveDirectoryMetricCollector(new ThresholdsConfiguration
        {
            PasswordMaxAgeDays = 31
        });

        var action = async () => await collector.CollectAsync();

        await action.Should().NotThrowAsync();

        var metrics = await collector.CollectAsync();
        if (metrics.Any())
        {
            metrics.Should().OnlyContain(m => m.Category == "ActiveDirectory");
        }
    }

    [Fact]
    public async Task WindowsUpdateCollector_CollectAsync_ShouldNotThrowAcrossEnvironments()
    {
        var collector = new WindowsUpdateMetricCollector();

        var action = async () => await collector.CollectAsync();

        await action.Should().NotThrowAsync();

        var metrics = await collector.CollectAsync();
        if (metrics.Any())
        {
            metrics.Should().OnlyContain(m => m.Category == "WindowsUpdate");
        }
    }
}
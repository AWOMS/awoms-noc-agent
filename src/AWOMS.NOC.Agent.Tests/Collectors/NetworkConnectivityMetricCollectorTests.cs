using AWOMS.NOC.Agent;
using AWOMS.NOC.Agent.Collectors;
using FluentAssertions;

namespace AWOMS.NOC.Agent.Tests.Collectors;

public class NetworkConnectivityMetricCollectorTests
{
    [Fact]
    public async Task CollectAsync_WithInvalidTarget_ShouldProduceStatusMetricWithoutThrowing()
    {
        var collector = new NetworkConnectivityMetricCollector(new AgentConfiguration
        {
            PingTargets = ["invalid-host-$$$", "   ", "invalid-host-$$$"]
        });

        var metrics = await collector.CollectAsync();

        metrics.Should().NotBeEmpty();
        metrics.Should().Contain(m => m.Name.Contains("Ping Status (invalid-host-$$$)"));
    }
}
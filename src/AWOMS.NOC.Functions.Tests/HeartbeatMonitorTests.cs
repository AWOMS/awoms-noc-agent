using AWOMS.NOC.Shared.Models;
using FluentAssertions;

namespace AWOMS.NOC.Functions.Tests;

public class HeartbeatMonitorTests
{
    [Fact]
    public void EvaluateMachineHeartbeat_OnlineAndTimedOut_ShouldCreateCriticalAlertAndMarkOffline()
    {
        var now = DateTime.UtcNow;
        var machine = new MachineEntity
        {
            AgentId = "a1",
            MachineName = "m1",
            LastHeartbeat = now.AddMinutes(-10),
            IsOnline = true
        };

        var result = AWOMS.NOC.Functions.HeartbeatMonitor.EvaluateMachineHeartbeat(
            machine,
            timeoutThreshold: now.AddMinutes(-5),
            heartbeatTimeoutMinutes: 5,
            nowUtc: now);

        result.ShouldUpdate.Should().BeTrue();
        result.NewIsOnline.Should().BeFalse();
        result.LastAlertSentUtc.Should().Be(now);
        result.Alert.Should().NotBeNull();
        result.Alert!.Severity.Should().Be("Critical");
        result.Alert.Category.Should().Be("Heartbeat");
    }

    [Fact]
    public void EvaluateMachineHeartbeat_OfflineAndRecovered_ShouldCreateWarningAlertAndMarkOnline()
    {
        var now = DateTime.UtcNow;
        var machine = new MachineEntity
        {
            AgentId = "a1",
            MachineName = "m1",
            LastHeartbeat = now.AddMinutes(-1),
            IsOnline = false
        };

        var result = AWOMS.NOC.Functions.HeartbeatMonitor.EvaluateMachineHeartbeat(
            machine,
            timeoutThreshold: now.AddMinutes(-5),
            heartbeatTimeoutMinutes: 5,
            nowUtc: now);

        result.ShouldUpdate.Should().BeTrue();
        result.NewIsOnline.Should().BeTrue();
        result.Alert.Should().NotBeNull();
        result.Alert!.Severity.Should().Be("Warning");
        result.Alert.MetricName.Should().Be("Heartbeat Recovered");
    }

    [Fact]
    public void EvaluateMachineHeartbeat_OfflineAndStillTimedOut_ShouldTakeNoAction()
    {
        var now = DateTime.UtcNow;
        var machine = new MachineEntity
        {
            AgentId = "a1",
            MachineName = "m1",
            LastHeartbeat = now.AddMinutes(-10),
            IsOnline = false
        };

        var result = AWOMS.NOC.Functions.HeartbeatMonitor.EvaluateMachineHeartbeat(
            machine,
            timeoutThreshold: now.AddMinutes(-5),
            heartbeatTimeoutMinutes: 5,
            nowUtc: now);

        result.ShouldUpdate.Should().BeFalse();
        result.Alert.Should().BeNull();
    }
}
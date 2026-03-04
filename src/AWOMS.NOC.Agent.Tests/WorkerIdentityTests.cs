using AWOMS.NOC.Agent;
using FluentAssertions;

namespace AWOMS.NOC.Agent.Tests;

public class WorkerIdentityTests
{
    [Fact]
    public void GenerateAgentId_WithSameInputs_ShouldBeDeterministic()
    {
        var id1 = Worker.GenerateAgentId("MachineA", "DomainA");
        var id2 = Worker.GenerateAgentId("MachineA", "DomainA");

        id1.Should().Be(id2);
    }

    [Fact]
    public void GenerateAgentId_WithDifferentInputs_ShouldBeDifferent()
    {
        var id1 = Worker.GenerateAgentId("MachineA", "DomainA");
        var id2 = Worker.GenerateAgentId("MachineB", "DomainA");

        id1.Should().NotBe(id2);
    }

    [Fact]
    public void GenerateAgentId_ShouldUseUrlSafeBase64Characters()
    {
        var id = Worker.GenerateAgentId("Machine+Slash/Test", "Domain=");

        id.Should().NotContain("+");
        id.Should().NotContain("/");
        id.Should().NotContain("=");
    }
}
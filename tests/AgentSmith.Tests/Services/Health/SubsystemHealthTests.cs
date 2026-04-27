using AgentSmith.Application.Services.Health;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Health;

public sealed class SubsystemHealthTests
{
    [Fact]
    public void Constructor_StartsInDownState()
    {
        var sut = new SubsystemHealth("queue_consumer");

        sut.Name.Should().Be("queue_consumer");
        sut.State.Should().Be(SubsystemState.Down);
        sut.Reason.Should().BeNull();
        sut.LastChangedUtc.Should().BeNull();
    }

    [Fact]
    public void SetUp_ChangesStateAndStampsLastChangedUtc()
    {
        var sut = new SubsystemHealth("redis");

        sut.SetUp();

        sut.State.Should().Be(SubsystemState.Up);
        sut.Reason.Should().BeNull();
        sut.LastChangedUtc.Should().NotBeNull();
    }

    [Fact]
    public void SetDegraded_StoresReason()
    {
        var sut = new SubsystemHealth("redis");

        sut.SetDegraded("connecting");

        sut.State.Should().Be(SubsystemState.Degraded);
        sut.Reason.Should().Be("connecting");
    }

    [Fact]
    public void SetDown_TouchesLastChangedUtc()
    {
        var sut = new SubsystemHealth("queue_consumer");
        sut.SetUp();
        var firstChange = sut.LastChangedUtc;
        Thread.Sleep(2);

        sut.SetDown("redis lost");

        sut.LastChangedUtc.Should().NotBeNull();
        sut.LastChangedUtc!.Value.Should().BeAfter(firstChange!.Value);
    }

    [Fact]
    public void SetDisabled_ReportsConfigGap()
    {
        var sut = new SubsystemHealth("queue_consumer");

        sut.SetDisabled("REDIS_URL not configured");

        sut.State.Should().Be(SubsystemState.Disabled);
        sut.Reason.Should().Be("REDIS_URL not configured");
    }

    [Fact]
    public void Set_SameStateAndReason_DoesNotChangeTimestamp()
    {
        var sut = new SubsystemHealth("redis");
        sut.SetUp();
        var firstChange = sut.LastChangedUtc;
        Thread.Sleep(2);

        sut.SetUp();

        sut.LastChangedUtc.Should().Be(firstChange);
    }
}

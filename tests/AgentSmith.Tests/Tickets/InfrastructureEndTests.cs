using AgentSmith.Contracts.Sandbox;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;
using FluentAssertions;

namespace AgentSmith.Tests.Tickets;

/// <summary>2026-10-13-9a59a: the one rule for an attempt that ended on infrastructure.</summary>
public sealed class InfrastructureEndTests
{
    [Theory]
    [InlineData("cancelled", "interrupted", true)]
    [InlineData("cancelled", "stale-lease-reaped", true)]
    [InlineData("cancelled", "operator", false)]
    [InlineData("cancelled", null, false)]
    [InlineData("failed", "interrupted", false)]
    public void InfrastructureEnd_InterruptedOrStaleLease_Holds_UserCancelDoesNot(
        string status, string? reason, bool holds)
    {
        var run = new Run { Id = "r", Status = status, CancelReason = reason };

        new InfrastructureEnd().Holds(run).Should().Be(holds);
    }

    // 2026-10-13-9a59n: the sandbox arm — a vanished or OOM-killed sandbox inside the run's last launch.
    [Fact]
    public void InfrastructureEnd_SandboxVanishedInLastSegment_Holds() =>
        Holds("failed", Launch(10, 30, Sandbox(SandboxEndReasons.Vanished, 12))).Should().BeTrue();

    [Fact]
    public void InfrastructureEnd_SandboxVanishedInEarlierSegment_DoesNotHold() =>
        Holds("failed", Launch(10, 30, Sandbox(SandboxEndReasons.Vanished, 4))).Should().BeFalse(
            "the sandbox was created in an earlier launch, which a relaunch went past");

    [Fact]
    public void InfrastructureEnd_OomKilledInLastSegment_Holds() =>
        Holds("cancelled", Launch(10, null, Sandbox(SandboxEndReasons.OomKilled, 11))).Should().BeTrue();

    [Fact]
    public void InfrastructureEnd_OomInLastSegment_SucceededRun_DoesNotHold() =>
        Holds("success", Launch(10, 30, Sandbox(SandboxEndReasons.OomKilled, 12))).Should().BeFalse(
            "a delivered run did not end on its OOM'd sandbox");

    [Fact]
    public void InfrastructureEnd_SandboxWithoutPosition_DoesNotHold() =>
        Holds("failed", Launch(10, 30, Sandbox(SandboxEndReasons.Vanished, null))).Should().BeFalse();

    [Theory]
    [InlineData(SandboxEndReasons.Gone)]
    [InlineData(SandboxEndReasons.Exited)]
    [InlineData(SandboxEndReasons.ReleasedAtEnd)]
    public void InfrastructureEnd_OtherSandboxEnds_DoNotHold(string reason) =>
        Holds("failed", Launch(10, 30, Sandbox(reason, 12))).Should().BeFalse();

    [Fact]
    public void InfrastructureEnd_SandboxAfterTheSegmentEnded_DoesNotHold() =>
        Holds("failed", Launch(10, 30, Sandbox(SandboxEndReasons.Vanished, 31))).Should().BeFalse();

    private static bool Holds(string status, RunLastLaunch launch) =>
        new InfrastructureEnd().Holds(new Run { Id = "r", Status = status }, launch);

    private static RunLastLaunch Launch(long start, long? end, RunSandbox sandbox) =>
        new(new RunSegment { RunId = "r", EventSeq = start, EndSeq = end }, [sandbox]);

    private static RunSandbox Sandbox(string reason, long? seq) =>
        new() { RunId = "r", Key = "api", RepoName = "api", EndReason = reason, EventSeq = seq };
}

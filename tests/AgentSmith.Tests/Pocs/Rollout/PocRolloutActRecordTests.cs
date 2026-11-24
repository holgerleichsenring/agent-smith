using AgentSmith.Application.Services.Pocs.Rollout;
using AgentSmith.Contracts.Pocs;
using AgentSmith.Contracts.Providers;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Rollout;

/// <summary>2026-11-23-c81ed: how a lifecycle act's follow ends — state from the desired replicas, the rollout state only
/// for a digest-changing act, nothing on a failed act but a rollback, and a settled delete frees the name.</summary>
public sealed class PocRolloutActRecordTests
{
    private readonly FakeRolloutStore _store = new();

    private static readonly PocRolloutBudget Budget = new(DateTimeOffset.UnixEpoch, "pocs.rollout.deadline_seconds (600 s)");

    private Task<AgentSmith.Domain.Models.CommandResult> EndAsync(
        string? act, PocRolloutReadingKind kind, int replicas = 1, bool absent = false) =>
        new PocRolloutRecord(_store, TimeProvider.System).EndAsync(new PocRolloutEnd(7, "todo", 2, "todo.example.internal",
            absent ? PocRolloutExpected.Gone("poc-todo") : PocRolloutVerdictTests.Expected(replicas),
            new PocRolloutReading(kind, "a reading", kind == PocRolloutReadingKind.Failed ? "Degraded" : null), Budget, act), default);

    [Fact]
    public async Task End_ASettledDelete_DeletesThePocAndFreesItsName()
    {
        var result = await EndAsync(PocActKinds.Delete, PocRolloutReadingKind.Settled, absent: true);

        result.IsSuccess.Should().BeTrue(result.Message);
        _store.Deleted.Should().Equal(7);
        _store.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task End_ALateDelete_WritesNothing_AndSaysItStaysPending()
    {
        var result = await EndAsync(PocActKinds.Delete, PocRolloutReadingKind.Waiting, absent: true);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("stays pending");
        (_store.Deleted.Count, _store.Outcomes.Count).Should().Be((0, 0));
    }

    [Theory]
    [InlineData(PocActKinds.Apply)]
    [InlineData(PocActKinds.Rollback)]
    public async Task End_ASettledActWithZeroDesiredReplicas_SetsStopped(string act)
    {
        await EndAsync(act, PocRolloutReadingKind.Settled, replicas: 0);

        _store.Outcomes.Single().PocState.Should().Be(PocStates.Stopped);
    }

    [Theory]
    [InlineData(PocActKinds.Stop, 0)]
    [InlineData(PocActKinds.Start, 1)]
    [InlineData(PocActKinds.Apply, 1)]
    public async Task End_ASettledActThatKeptTheDigest_LeavesTheRolloutStateUntouched(string act, int replicas)
    {
        await EndAsync(act, PocRolloutReadingKind.Settled, replicas);

        _store.Outcomes.Single().Should().Match<PocRolloutOutcome>(o =>
            o.RolloutState == null && o.PocState == (replicas > 0 ? PocStates.Ready : PocStates.Stopped));
    }

    [Fact]
    public async Task End_ASettledRollback_MarksTheRolledToRevisionHealthy()
    {
        await EndAsync(PocActKinds.Rollback, PocRolloutReadingKind.Settled);

        _store.Outcomes.Single().Should().Match<PocRolloutOutcome>(o => o.RolloutState == PocRolloutStates.Healthy && o.PocState == PocStates.Ready);
    }

    [Theory]
    [InlineData(PocActKinds.Stop)]
    [InlineData(PocActKinds.Apply)]
    public async Task End_AFailedStopOrApply_LeavesTheStateAndTheRolloutStateUntouched(string act)
    {
        var result = await EndAsync(act, PocRolloutReadingKind.Failed);

        result.IsSuccess.Should().BeFalse();
        result.Message.Should().Contain("left as they were");
        _store.Outcomes.Should().BeEmpty();
    }

    [Fact]
    public async Task End_AFailedRollback_MarksTheRolledToRevisionFailed_AndKeepsTheState()
    {
        await EndAsync(PocActKinds.Rollback, PocRolloutReadingKind.Failed);

        _store.Outcomes.Single().Should().Match<PocRolloutOutcome>(o =>
            o.Revision == 2 && o.RolloutState == PocRolloutStates.Failed && o.PocState == null);
    }

    [Fact]
    public void Judge_AStopAtZero_SettlesWithNoReadyPod() =>
        new PocRolloutVerdict().Judge(PocRolloutVerdictTests.Expected(replicas: 0),
                new ArgoApplicationState("Synced", "Healthy", null, "Succeeded", null),
                PocRolloutVerdictTests.Live(replicas: 0, ready: 0, updated: 0))
            .Kind.Should().Be(PocRolloutReadingKind.Settled);
}

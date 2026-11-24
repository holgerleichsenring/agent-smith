using AgentSmith.Application.Services.Pocs.Acts;
using AgentSmith.Contracts.Pocs;
using FluentAssertions;

namespace AgentSmith.Tests.Pocs.Acts;

/// <summary>2026-11-23-c81ed: the one table of the lifecycle acts and the tri-state readiness seed.</summary>
public sealed class PocActPlanTests
{
    internal const string Current = "sha256:2222222222222222222222222222222222222222222222222222222222222222";
    internal const string Older = "sha256:1111111111111111111111111111111111111111111111111111111111111111";

    private readonly PocActPlan _plan = new();

    internal static PocActSubject Row(string state = PocStates.Ready, int replicas = 1, int? revision = 2, string? digest = Current,
        string? readiness = "/health") => new(7, "shop", "main", state, replicas, revision, digest, readiness);

    private static readonly PocActRevision HealthyOlder = new(1, Older, "abc", PocRolloutStates.Healthy);

    [Fact]
    public void Decide_StopFromReady_WantsZeroReplicas()
    {
        var decision = _plan.Decide(new PocActOrder(PocActKinds.Stop), Row(), null, 256);

        decision.Desired.Should().Be(new PocDesiredState(0, 2, Current, "/health"));
    }

    [Fact]
    public void Decide_StartFromReady_IsRefusedNamingTheState()
    {
        var decision = _plan.Decide(new PocActOrder(PocActKinds.Start), Row(), null, 256);

        decision.Refusal.Should().Contain("start").And.Contain("ready").And.Contain("stopped");
    }

    [Fact]
    public void Decide_StartFromStopped_WantsOneReplica() =>
        _plan.Decide(new PocActOrder(PocActKinds.Start), Row(PocStates.Stopped, 0), null, 256).Desired!.Replicas.Should().Be(1);

    [Fact]
    public void Decide_RollbackToAHealthyOlderRevision_KeepsReplicas_AndTakesItsDigest()
    {
        var decision = _plan.Decide(new PocActOrder(PocActKinds.Rollback, 1), Row(PocStates.Stopped, 0), HealthyOlder, 256);

        decision.Desired.Should().Be(new PocDesiredState(0, 1, Older, "/health"));
    }

    [Fact]
    public void Decide_RollbackToANeverHealthyRevision_IsRefused() =>
        _plan.Decide(new PocActOrder(PocActKinds.Rollback, 1), Row(), HealthyOlder with { RolloutState = PocRolloutStates.Failed }, 256)
            .Refusal.Should().Contain("never rolled out healthy");

    [Fact]
    public void Decide_RollbackToTheCurrentRevision_IsRefused() =>
        _plan.Decide(new PocActOrder(PocActKinds.Rollback, 2), Row(), new PocActRevision(2, Current, "def", PocRolloutStates.Healthy), 256)
            .Refusal.Should().Contain("already runs revision 2");

    [Fact]
    public void Decide_Delete_WantsTheAbsence_FromAnyStateButDeleted()
    {
        _plan.Decide(new PocActOrder(PocActKinds.Delete), Row(PocStates.Blocked, revision: null, digest: null), null, 256).Absent.Should().BeTrue();
        _plan.Decide(new PocActOrder(PocActKinds.Delete), Row(PocStates.Ready, revision: null, digest: null), null, 256).Absent.Should().BeTrue();
        _plan.Decide(new PocActOrder(PocActKinds.Delete), Row(PocStates.Deleted), null, 256).Refusal.Should().Contain("deleted already");
    }

    [Theory]
    [InlineData(PocStates.Deleted)]
    [InlineData(PocStates.Importing)]
    [InlineData(PocStates.Refused)]
    [InlineData(PocStates.Blocked)]
    public void Decide_RollbackAndApply_FromAStateThatNeverDeployed_AreRefusedNamingIt(string state)
    {
        _plan.Decide(new PocActOrder(PocActKinds.Rollback, 1), Row(state), HealthyOlder, 256).Refusal.Should().Contain($"is {state}");
        _plan.Decide(new PocActOrder(PocActKinds.Apply), Row(state), null, 256).Refusal.Should().Contain($"is {state}");
    }

    [Theory]
    [InlineData(PocActKinds.Stop)]
    [InlineData(PocActKinds.Rollback)]
    [InlineData(PocActKinds.Apply)]
    public void Decide_AnyActButDelete_WithoutARevision_IsRefused(string act)
    {
        var decision = _plan.Decide(new PocActOrder(act, 1), Row(revision: null, digest: null), HealthyOlder, 256);

        decision.Refusal.Should().Contain("no deployed revision").And.Contain("delete is pending");
    }

    [Fact]
    public void Decide_ApplyOnAStoppedPoc_KeepsZeroReplicas() =>
        _plan.Decide(new PocActOrder(PocActKinds.Apply), Row(PocStates.Stopped, 0), null, 256).Desired!.Replicas.Should().Be(0);

    [Fact]
    public void Readiness_AbsentKeeps_TcpClears_APathSets()
    {
        _plan.Decide(new PocActOrder(PocActKinds.Apply), Row(), null, 256).Desired!.ReadinessPath.Should().Be("/health");
        _plan.Decide(new PocActOrder(PocActKinds.Apply, Readiness: "tcp"), Row(), null, 256).Desired!.ReadinessPath.Should().BeNull();
        _plan.Decide(new PocActOrder(PocActKinds.Apply, Readiness: "/healthz"), Row(), null, 256).Desired!.ReadinessPath.Should().Be("/healthz");
    }

    [Fact]
    public void Readiness_WithoutASlashOrOverTheBound_IsRefusedNamingTheKey()
    {
        _plan.Readiness("healthz", 256).Refusal.Should().Contain("'healthz'").And.Contain("starts with '/'");
        _plan.Readiness("/" + new string('a', 20), 8).Refusal.Should().Contain("21 characters").And.Contain("at most 8")
            .And.Contain("pocs.readiness_path_max_chars");
    }

    [Fact]
    public void WhyNot_AnUnknownAct_NamesTheActs() =>
        _plan.WhyNot("restart", Row()).Should().Contain("not a PoC act").And.Contain("stop, start, rollback, apply, delete");
}

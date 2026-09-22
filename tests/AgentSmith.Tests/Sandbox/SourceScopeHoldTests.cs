using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: a design conversation's sandbox survives the turn that spawned it, and
/// the next turn wraps it in a FRESH scope — so only the first message pays a container spawn
/// and a clone, while everything the scope reports is still this turn's.
/// </summary>
public sealed class SourceScopeHoldTests
{
    private readonly SourceScopeHoldFixture _fixture = new();

    [Fact]
    public async Task Dialog_ASecondTurnOfOneConversation_SpawnsNoContainerAndClonesNothing()
    {
        var scopes = _fixture.Scopes(Holds.Live());

        await _fixture.TurnAsync(scopes);
        await _fixture.TurnAsync(scopes);

        _fixture.Spawner.Spawned.Should().ContainSingle(
            "the second turn takes the sandbox the first one left");
        var sandbox = _fixture.Spawner.Only;
        sandbox.Commands.Count(c => c.Contains("clone", StringComparison.Ordinal)).Should().Be(1,
            "the tree is already there, so the second turn refreshes it instead of cloning");
        sandbox.Ran("fetch --depth 1 origin HEAD").Should().BeTrue();
        sandbox.Ran("reset --hard FETCH_HEAD").Should().BeTrue();
        sandbox.Disposed.Should().BeFalse("a held sandbox is released, never disposed");
    }

    [Fact]
    public async Task Dialog_ASecondTurn_WrapsTheHeldSandboxInAFreshScope()
    {
        var scopes = _fixture.Scopes(Holds.Live());
        var first = await _fixture.TurnAsync(scopes);

        var second = await _fixture.TurnAsync(scopes);

        second.Scope.Should().NotBeSameAs(first.Scope,
            "a reused scope object caches its inner sandbox before it refreshes or reports");
        second.JobId.Should().Be(first.JobId, "and yet it reads through the very same container");
        second.Materialised.Should().BeTrue();
    }

    [Fact]
    public async Task Dialog_ASecondTurnOnARefreshedTree_ReportsTheShaItLandedOnNotThePreviousOne()
    {
        var scopes = _fixture.Scopes(Holds.Live());
        var first = await _fixture.TurnAsync(scopes);
        _fixture.Spawner.Only.RefreshedHead = "sha-after";

        var second = await _fixture.TurnAsync(scopes);

        first.Sha.Should().Be("sha-before");
        second.Sha.Should().Be("sha-after",
            "the sha a filed ticket's provenance carries is the one this turn actually read");
    }

    [Fact]
    public async Task Dialog_ASecondTurn_StillPushesOpeningAndReadyToTheDashboard()
    {
        var scopes = _fixture.Scopes(Holds.Live());
        var observer = new RecordingScopeObserver();
        using var observing = _fixture.Observers.Observe(observer);

        await _fixture.TurnAsync(scopes);
        await _fixture.TurnAsync(scopes);

        observer.Reports.Should().Equal(
            ("repo-a", SourceScopeProgress.Opening), ("repo-a", SourceScopeProgress.Ready),
            ("repo-a", SourceScopeProgress.Opening), ("repo-a", SourceScopeProgress.Ready));
    }

    [Fact]
    public async Task Dialog_AHeldSandboxWithNoHeartbeat_IsDroppedAndTheTurnSpawnsAfresh()
    {
        var scopes = _fixture.Scopes(Holds.None());

        await _fixture.TurnAsync(scopes);
        var second = await _fixture.TurnAsync(scopes);

        _fixture.Spawner.Spawned.Should().HaveCount(2, "a sandbox with no heartbeat is gone");
        _fixture.Spawner.Spawned[0].Sandbox.ForceRemovedAt.Should().NotBeNull(
            "the corpse is taken away rather than left for the hold window to lapse");
        _fixture.Spawner.Spawned[1].Sandbox.Ran("clone").Should().BeTrue();
        second.Sha.Should().Be("sha-before", "the turn answers normally, only slowly");
    }
}

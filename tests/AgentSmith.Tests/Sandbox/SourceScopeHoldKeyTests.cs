using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: WHICH sandbox a turn may take back. The key is the conversation, the
/// repository and the revision together, because a turn may address one repository twice —
/// its own scope at the clone's default and a template of it pinned to a tag — and no
/// conversation may ever be handed another's tree.
/// </summary>
public sealed class SourceScopeHoldKeyTests
{
    private readonly SourceScopeHoldFixture _fixture = new();

    [Fact]
    public async Task Dialog_AScopeThatNeverMaterialised_IsNotRegistered()
    {
        var register = Holds.Live();
        var scopes = _fixture.Scopes(register);

        await scopes.Create(
                SourceScopeHoldFixture.Project, SourceScopeHoldFixture.Repo(),
                revision: null, SourceScopeHoldFixture.Conversation)
            .DisposeAsync();

        _fixture.Spawner.Spawned.Should().BeEmpty("a scope that served no read never spawned");
        (await register.TakeAsync(
                HeldSandbox.KeyFor(SourceScopeHoldFixture.Conversation, "repo-a", null),
                CancellationToken.None))
            .Should().BeNull(
                "registering it would hand the reaper's rail and the eviction a job id that "
                + "matches no container");
    }

    [Fact]
    public async Task Dialog_TwoConversationsOverOneRepo_HoldTwoSandboxesAndSeeSeparateTrees()
    {
        var scopes = _fixture.Scopes(Holds.Live());

        var mine = await _fixture.TurnAsync(scopes);
        var yours = await _fixture.TurnAsync(scopes, conversation: "9f8e7d6c");
        var mineAgain = await _fixture.TurnAsync(scopes);

        _fixture.Spawner.Spawned.Should().HaveCount(2,
            "one sandbox per conversation over the repository, not one shared between them");
        yours.JobId.Should().NotBe(mine.JobId, "no conversation can read another's tree");
        mineAgain.JobId.Should().Be(mine.JobId, "and each takes its own back");
    }

    [Fact]
    public async Task Dialog_OneTurnAddressingOneRepoAtTwoRevisions_HoldsTwoSandboxes()
    {
        var scopes = _fixture.Scopes(Holds.Live());

        var free = await _fixture.TurnAsync(scopes);
        var pinned = await _fixture.TurnAsync(scopes, revision: "v1.4.0");
        var pinnedAgain = await _fixture.TurnAsync(scopes, revision: "v1.4.0");

        _fixture.Spawner.Spawned.Should().HaveCount(2,
            "a pinned template and the same repository at its default branch are two trees");
        pinned.JobId.Should().NotBe(free.JobId);
        pinnedAgain.JobId.Should().Be(pinned.JobId, "each is taken back under its own key");
    }
}

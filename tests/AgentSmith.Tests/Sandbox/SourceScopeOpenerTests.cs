using AgentSmith.Application.Services.Builders;
using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11b: spawning a sandbox and preparing a tree in it are two acts, because a
/// held sandbox needs the second without the first.
/// </summary>
public sealed class SourceScopeOpenerTests
{
    private static readonly ResolvedProject Project = new() { Name = "p" };

    [Fact]
    public async Task Opener_Spawn_MakesTheContainerAndPutsNothingInIt()
    {
        var factory = new StubSandboxFactory();

        var spawned = await Opener(factory).SpawnAsync(Project, CancellationToken.None, "a1b2c3d4");

        factory.Spawned.Should().ContainSingle().Which.Spec.ConversationId.Should().Be("a1b2c3d4",
            "a held sandbox that carries no conversation label is reaped within thirty seconds");
        ((StubSandbox)spawned).RanSteps.Should().BeEmpty("a spawn clones nothing");
    }

    [Fact]
    public async Task Opener_Prepare_OverASandboxThatIsAlreadyThere_LandsTheTreeAndReportsTheSha()
    {
        var factory = new StubSandboxFactory();
        var opener = Opener(factory);
        var spawned = await opener.SpawnAsync(Project, CancellationToken.None);

        var sha = await opener.PrepareAsync(spawned, Repo(), revision: null, CancellationToken.None);

        factory.Spawned.Should().ContainSingle("preparing spawns nothing of its own");
        sha.Should().Be("stub-head");
        ((StubSandbox)spawned).RanSteps.Should().NotBeEmpty();
    }

    private static SourceScopeOpener Opener(StubSandboxFactory factory)
    {
        var specBuilder = new SandboxSpecBuilder(
            new StubSandboxResourceResolver(),
            Mock.Of<IAgentImageResolver>(r => r.Resolve(It.IsAny<ResolvedProject>()) == "agent:test"));
        return new SourceScopeOpener(
            new SourceScopeMaterialiser(new SourceScopeRefresh()), factory, specBuilder,
            Mock.Of<IRunContextAccessor>());
    }

    private static RepoConnection Repo() =>
        new() { Name = "repo-a", Type = RepoType.GitHub, Url = "https://stub.test/repo-a" };
}

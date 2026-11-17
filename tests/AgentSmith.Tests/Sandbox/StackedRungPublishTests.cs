using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-17-9a39b: a stacked prerequisite's branch is found, never published.</summary>
public sealed class StackedRungPublishTests
{
    private const string ParentTicket = "4711";
    private const string Rung = "agent-smith/4711";

    private static readonly RepoConnection Repo =
        new() { Name = "server", Type = RepoType.GitHub, Url = "https://example.invalid/repo.git" };

    [Fact]
    public async Task RungPublish_AStackedRungThatIsMissing_IsNeverPushedAndFallsThrough()
    {
        var sandbox = new RungPushSandbox(Rung, "main");

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, ParentTicket, CancellationToken.None, publish: false);

        resolved.FellThrough.Should().BeTrue();
        resolved.Name.Should().Be("main");
        sandbox.Pushed.Should().BeNull("a landed prerequisite's deleted branch must not come back at the clone's base");
    }

    [Fact]
    public async Task RungPublish_AStackedRungThatExists_IsTheBase()
    {
        var sandbox = new RungPushSandbox(Rung, "main", Rung);

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, ParentTicket, CancellationToken.None, publish: false);

        resolved.Name.Should().Be(Rung);
        resolved.FellThrough.Should().BeFalse();
    }
}

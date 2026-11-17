using AgentSmith.Application.Services;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-13-35a4: a feature's branch is published exactly once per repository, and the
/// push that publishes it carries NO force and NO lease — the one property that separates
/// this path from the work-branch pusher, whose stale-info retry would force a shared
/// branch back to the base and delete everything already merged into it.
/// </summary>
public sealed class RungPublishTests
{
    private const string ParentTicket = "4711";
    private const string Rung = "agent-smith/4711";

    private static readonly RepoConnection Repo =
        new() { Name = "server", Type = RepoType.GitHub, Url = "https://example.invalid/repo.git" };

    [Fact]
    public async Task RungPublish_AlreadyExists_AdoptsItAndDoesNotPush()
    {
        var sandbox = new RungPushSandbox(Rung, "main", Rung);

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(Rung);
        resolved.FellThrough.Should().BeFalse();
        sandbox.Pushed.Should().BeNull(
            "a branch that is already there is adopted — publishing is idempotent, and the "
            + "cheapest idempotence is not pushing at all");
    }

    [Fact]
    public async Task RungPublish_NeverUsesForceOrLease()
    {
        var sandbox = new RungPushSandbox(Rung, "main");

        await TestGit.RungPublisher.EnsureAsync(sandbox, Repo, ParentTicket, CancellationToken.None);

        var pushed = sandbox.Pushed;
        pushed.Should().NotBeNull("the rung is absent, so this slice publishes it");
        pushed.Should().NotContain(a => a.StartsWith("--force", StringComparison.Ordinal) || a == "-f",
            "a lease that can be refreshed by a fetch is how a shared branch loses the work "
            + "merged into it — the rejection IS the answer here");
        pushed.Should().Contain($"origin/main:refs/heads/{Rung}",
            "the rung is created at the sha of the ladder's next rung down");
    }

    [Fact]
    public async Task RungPublish_NoParentStamp_PublishesNothing()
    {
        var sandbox = new RungPushSandbox(Rung, "main");

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, parentTicketId: null, CancellationToken.None);

        resolved.Name.Should().Be("main");
        resolved.FellThrough.Should().BeTrue();
        sandbox.Pushed.Should().BeNull(
            "a ticket that is not a slice has no feature to publish, so a repository no "
            + "slice touches never grows a branch");
    }

    [Fact]
    public async Task RungPublish_PublishedRung_CarriesTheFrameworkBranchConvention()
    {
        var sandbox = new RungPushSandbox(Rung, "main");

        var resolved = await TestGit.RungPublisher.EnsureAsync(
            sandbox, Repo, ParentTicket, CancellationToken.None);

        resolved.Name.Should().Be(TicketBranchNamer.Compose(ParentTicket).Value,
            "the name the publisher composes must be the name a later slice looks for");
    }
}

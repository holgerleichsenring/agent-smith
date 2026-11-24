using AgentSmith.Application.Services.Handlers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Commands;

/// <summary>2026-11-23-31dce: a merge-green run reads each pull request's base from the host and refuses one that does not
/// target the default branch — the configured one, or with none configured any agent-smith work branch — or whose
/// base cannot be read; a local repository has none.</summary>
public sealed class PullRequestBaseGuardTests
{
    private const string Url = "https://x/a/pull/1";

    [Theory]
    [InlineData("main", "main", false)]
    [InlineData("main", "agent-smith/41", true)]
    [InlineData("main", "develop", true)]
    [InlineData(null, "agent-smith/41", true)]
    [InlineData(null, "develop", false)]
    public async Task RefusalAsync_ComparesTheHostsBase_WithTheDefaultBranch(string? main, string current, bool refused)
    {
        var refusal = await GuardReading(current).RefusalAsync(new RepoConnection { Name = "a", DefaultBranch = main }, Url, default);

        (refusal is not null).Should().Be(refused);
        if (refused) refusal.Should().Contain($"targets '{current}', not the default branch");
    }

    // 2026-11-24-f9c5c: the merge's series branch is a base it accepts; another agent-smith branch is still refused.
    [Theory]
    [InlineData("agent-smith/series/sr-1", false)]
    [InlineData("main", false)]
    [InlineData("agent-smith/series/sr-2", true)]
    [InlineData("agent-smith/41", true)]
    public async Task RefusalAsync_TheMergesSeriesBranch_Passes(string current, bool refused)
    {
        var refusal = await GuardReading(current).RefusalAsync(
            new RepoConnection { Name = "a", DefaultBranch = "main" }, Url, default, "agent-smith/series/sr-1");

        (refusal is not null).Should().Be(refused);
    }

    [Fact]
    public async Task RefusalAsync_AHostedBaseThatCannotBeRead_IsRefused()
    {
        var refusal = await GuardReading(null).RefusalAsync(new RepoConnection { Name = "a", DefaultBranch = "main" }, Url, default);

        refusal.Should().StartWith("its base could not be read from the host");
    }

    [Fact]
    public async Task RefusalAsync_ALocalRepository_HasNoBaseToRefuse()
    {
        var refusal = await GuardReading("agent-smith/41").RefusalAsync(new RepoConnection { Name = "a", Type = RepoType.Local }, Url, default);

        refusal.Should().BeNull();
    }

    private static PullRequestBaseGuard GuardReading(string? current)
    {
        var provider = new Mock<ISourceProvider>();
        provider.Setup(p => p.ReadPullRequestBaseAsync(Url, It.IsAny<CancellationToken>())).ReturnsAsync(current);
        var factory = new Mock<ISourceProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        return new PullRequestBaseGuard(factory.Object);
    }
}

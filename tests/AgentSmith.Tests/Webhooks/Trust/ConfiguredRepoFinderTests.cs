using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.Webhooks;
using FluentAssertions;

namespace AgentSmith.Tests.Webhooks.Trust;

public sealed class ConfiguredRepoFinderTests
{
    private static readonly AgentSmithConfig Config = new()
    {
        Projects = new()
        {
            ["api"] = new ResolvedProject
            {
                Repos = [new RepoConnection { Name = "my-api", Url = "https://dev.azure.com/org/MyProject/_git/my-api" }],
            },
        },
    };

    private readonly ConfiguredRepoFinder _sut = new();

    [Fact]
    public void Find_PayloadCloneUrlWithUserInfo_MatchesTheConfiguredWebUrl()
    {
        var match = _sut.Find(Config, "https://org@dev.azure.com/org/MyProject/_git/my-api.git");

        match.Should().NotBeNull();
        match!.ProjectName.Should().Be("api");
        match.Repo.Name.Should().Be("my-api");
    }

    [Fact]
    public void Find_AnUnconfiguredRepository_MatchesNothing() =>
        _sut.Find(Config, "https://dev.azure.com/org/MyProject/_git/other").Should().BeNull();

    [Fact]
    public void Find_AnEmptyUrl_MatchesNothing() =>
        _sut.Find(Config, "").Should().BeNull();
}

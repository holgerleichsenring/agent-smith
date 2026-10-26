using AgentSmith.Application.Services.Configuration;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Factories;
using FluentAssertions;

namespace AgentSmith.Tests.Factories;

/// <summary>2026-10-25-9dbbg: only a trailing ".git" is a suffix; a name containing ".git" resolves to itself.</summary>
public sealed class RepoUrlGitSuffixTests
{
    [Fact]
    public void ResolveGitLabTarget_ProjectNamedGitops_KeepsName()
    {
        var (_, path, clone) = SourceProviderFactory.ResolveGitLabTarget("https://gitlab.example/team/my.gitops", null);

        path.Should().Be("team/my.gitops");
        clone.Should().Be("https://gitlab.example/team/my.gitops.git");
    }

    [Fact]
    public void ResolveGitLabTarget_CloneUrl_DropsTrailingGit() =>
        SourceProviderFactory.ResolveGitLabTarget("https://gitlab.example/team/my.gitops.git", null)
            .ProjectPath.Should().Be("team/my.gitops");

    [Fact]
    public void AzureDevOpsRepoAddress_RepoNamedDotGitSomething_KeepsName()
    {
        var address = AzureDevOpsRepoAddress.Parse("https://dev.azure.com/org/proj/_git/app.github.io");

        address.Project.Should().Be("proj");
        address.Repo.Should().Be("app.github.io");
    }

    [Fact]
    public void AzureDevOpsRepoAddress_CloneUrl_DropsTrailingGit() =>
        AzureDevOpsRepoAddress.Parse("https://dev.azure.com/org/proj/_git/my.gitops.git")
            .Repo.Should().Be("my.gitops");

    [Fact]
    public void TriggerModeFindings_RepoNamedGitops_KeyKeepsName()
    {
        var repo = new RepoConnection { Name = "r", Url = "https://gitlab.example/team/my.gitops", Type = RepoType.GitLab };
        ResolvedProject[] projects = [Project("p", repo, polls: false), Project("q", repo, polls: true)];
        var config = new AgentSmithConfig
        {
            Projects = projects.ToDictionary(p => p.Name),
            Trackers = projects.ToDictionary(p => p.Tracker.Name, p => p.Tracker),
        };

        TriggerModeFindings.Findings(config).Should().ContainSingle(f => f.Reason.Contains("gitlab.example/team/my.gitops "));
    }

    [Theory]
    [InlineData("https://github.com/o/app.github.io", "https://github.com/o/app.github.io")]
    [InlineData("https://github.com/o/r.git/", "https://github.com/o/r")]
    [InlineData("https://github.com/o/r.GIT", "https://github.com/o/r")]
    public void WithoutGitSuffix_OnlyTrailing(string url, string expected) =>
        RepoKey.WithoutGitSuffix(url).Should().Be(expected);

    private static ResolvedProject Project(string name, RepoConnection repo, bool polls) => new()
    {
        Name = name, Repos = [repo],
        Tracker = new TrackerConnection { Name = $"{name}-tracker", Type = TrackerType.GitLab, Polling = new PollingConfig { Enabled = polls } },
    };
}

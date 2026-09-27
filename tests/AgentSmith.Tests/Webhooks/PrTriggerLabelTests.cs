using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Webhooks;

/// <summary>
/// 2026-09-25-d83b: the word an operator puts on a pull request to ask for a review is
/// configured per project trigger on both hosts, the word every board carries today never
/// stops working, and a deployment that configures nothing behaves as it did before.
/// </summary>
public sealed class PrTriggerLabelTests
{
    private const string ConfigPath = "test-config.yml";
    private const string GitHubRepo = "https://github.com/org/my-api";
    private const string GitLabRepo = "https://gitlab.com/org/my-api";

    private static readonly IDictionary<string, string> EmptyHeaders =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task PrTrigger_AConfiguredWord_StartsTheReview()
    {
        var config = Config("please-review");

        var github = await GitHub(config).HandleAsync(GitHubPayload("please-review"), EmptyHeaders);
        var gitlab = await GitLab(config).HandleAsync(GitLabPayload("please-review"), EmptyHeaders);

        github.Handled.Should().BeTrue();
        github.Pipeline.Should().Be("security-scan");
        gitlab.Handled.Should().BeTrue();
        gitlab.ProjectName.Should().Be("my-api");
    }

    [Fact]
    public async Task PrTrigger_TheHistoricalWord_StillStartsTheReview()
    {
        var config = Config("please-review");

        var github = await GitHub(config).HandleAsync(
            GitHubPayload(PrTriggerLabelResolver.HistoricalLabel), EmptyHeaders);
        var gitlab = await GitLab(config).HandleAsync(
            GitLabPayload(PrTriggerLabelResolver.HistoricalLabel), EmptyHeaders);

        github.Handled.Should().BeTrue();
        gitlab.Handled.Should().BeTrue();
    }

    [Fact]
    public async Task PrTrigger_AnUnconfiguredDeployment_BehavesExactlyAsToday()
    {
        var config = Config(null);

        var githubTrigger = await GitHub(config).HandleAsync(
            GitHubPayload(PrTriggerLabelResolver.HistoricalLabel), EmptyHeaders);
        var githubOther = await GitHub(config).HandleAsync(GitHubPayload("needs-review"), EmptyHeaders);
        var gitlabTrigger = await GitLab(config).HandleAsync(
            GitLabPayload(PrTriggerLabelResolver.HistoricalLabel), EmptyHeaders);
        var gitlabOther = await GitLab(config).HandleAsync(GitLabPayload("needs-review"), EmptyHeaders);

        githubTrigger.Handled.Should().BeTrue();
        githubOther.Handled.Should().BeFalse();
        gitlabTrigger.Handled.Should().BeTrue();
        gitlabOther.Handled.Should().BeFalse();
    }

    /// <summary>The word is scoped to the project that owns the repo, so one board's
    /// vocabulary cannot start runs on another board's pull requests.</summary>
    [Fact]
    public async Task PrTrigger_AWordConfiguredForAnotherRepo_DoesNotStartTheReview()
    {
        var config = Config("please-review");

        var result = await GitHub(config).HandleAsync(
            GitHubPayload("please-review", "https://github.com/other/service"), EmptyHeaders);

        result.Handled.Should().BeFalse();
    }

    private static AgentSmithConfig Config(string? word) =>
        new()
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["my-api"] = new()
                {
                    Repos = new[]
                    {
                        new RepoConnection { Url = GitHubRepo },
                        new RepoConnection { Url = GitLabRepo },
                    },
                    GithubTrigger = new WebhookTriggerConfig { PrTriggerLabel = word },
                    GitlabTrigger = new WebhookTriggerConfig { PrTriggerLabel = word },
                },
            },
        };

    private static GitHubPrLabelWebhookHandler GitHub(AgentSmithConfig config) =>
        new(Loader(config), new ServerContext(ConfigPath), new PrTriggerLabelResolver(),
            NullLogger<GitHubPrLabelWebhookHandler>.Instance);

    private static GitLabMrLabelWebhookHandler GitLab(AgentSmithConfig config) =>
        new(Loader(config), new ServerContext(ConfigPath), new PrTriggerLabelResolver(),
            NullLogger<GitLabMrLabelWebhookHandler>.Instance);

    private static IConfigurationLoader Loader(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(c => c.LoadConfig(ConfigPath)).Returns(config);
        return loader.Object;
    }

    private static string GitHubPayload(string label, string repoUrl = GitHubRepo) => $$"""
        {
            "action": "labeled",
            "label": { "name": "{{label}}" },
            "pull_request": { "number": 7 },
            "repository": { "name": "my-api", "clone_url": "{{repoUrl}}.git" }
        }
        """;

    private static string GitLabPayload(string label, string repoUrl = GitLabRepo) => $$"""
        {
            "object_attributes": { "action": "update", "iid": 3 },
            "labels": [{ "title": "{{label}}" }],
            "project": { "path": "my-api", "web_url": "{{repoUrl}}" }
        }
        """;
}

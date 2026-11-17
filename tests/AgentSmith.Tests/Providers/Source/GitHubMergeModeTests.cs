using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// 2026-11-17-023fa: a code run's merge of a GitHub pull request. Bypass merges through REST as the token; a refusal
/// names branch protection, the admin or ruleset bypass to grant and the key. Respect enables auto-merge (GraphQL) with
/// a method the repository allows, and an auto-merge the repository turns off is a refusal naming allow_auto_merge.
/// </summary>
public sealed class GitHubMergeModeTests
{
    private const string PrUrl = "https://github.com/example/sample-server/pull/7";
    private static readonly BranchName Branch = new("agent-smith/42");

    [Fact]
    public async Task Bypass_Refused_NamesPolicyPermissionAndKey()
    {
        var pulls = new Mock<IPullRequestsClient>();
        pulls.Setup(c => c.Merge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MergePullRequest>()))
            .ThrowsAsync(NotMergeable("At least 1 approving review is required"));
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.PullRequest).Returns(pulls.Object);
        var repositories = new Mock<IRepositoriesClient>();
        repositories.Setup(r => r.Get("example", "sample-server")).ReturnsAsync(Repo());
        client.SetupGet(c => c.Repository).Returns(repositories.Object);

        var completion = await Sut(client.Object).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().StartWith("cannot bypass branch protection on GitHub:");
        completion.Reason.Should().Contain("approving review");
        completion.Reason.Should().Contain("ruleset's bypass list");
        completion.Reason.Should().EndWith("or set respect_branch_protection: true");
    }

    [Fact]
    public async Task Respect_EnablesAutoMerge_WithAnAllowedMethod_AndIsArmed()
    {
        var host = new GitHubGraphQlHost();
        var client = host.Client((query, _) => query.Contains("enablePullRequestAutoMerge")
            ? """{"data":{"enablePullRequestAutoMerge":{"clientMutationId":null}}}"""
            : """{"data":{"repository":{"mergeCommitAllowed":false,"squashMergeAllowed":true,"pullRequest":{"id":"PR_node7"}}}}""");

        var completion = await Sut(client).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Respect, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Armed);
        var mutation = host.Requests.Single(r => r.Query.Contains("enablePullRequestAutoMerge"));
        mutation.Variables.GetProperty("id").GetString().Should().Be("PR_node7");
        mutation.Variables.GetProperty("method").GetString().Should().Be("SQUASH");
    }

    [Fact]
    public async Task Respect_AutoMergeOff_IsRefusedNamingTheSetting()
    {
        var client = new GitHubGraphQlHost().Client((query, _) => query.Contains("enablePullRequestAutoMerge")
            ? """{"errors":[{"message":"Auto merge is not allowed for this repository"}]}"""
            : """{"data":{"repository":{"mergeCommitAllowed":true,"squashMergeAllowed":true,"pullRequest":{"id":"PR_node7"}}}}""");

        var completion = await Sut(client).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Respect, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("allow_auto_merge");
    }

    private static ISourceProvider Sut(IGitHubClient client)
    {
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<GitHubRepoAddress>(), It.IsAny<string>())).Returns(client);
        return new GitHubSourceProvider(new GitHubSourceConnection("https://github.com/example/sample-server", "ghp", "main"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);
    }

    private static Repository Repo() =>
        new(url: "", htmlUrl: "", cloneUrl: "", gitUrl: "", sshUrl: "", svnUrl: "",
            mirrorUrl: "", archiveUrl: "", id: 1, nodeId: "", owner: null!, name: "sample-server",
            fullName: "example/sample-server", isTemplate: false, description: "", homepage: "",
            language: "", @private: false, fork: false, forksCount: 0, stargazersCount: 0,
            defaultBranch: "main", openIssuesCount: 0, pushedAt: null,
            createdAt: default, updatedAt: default, permissions: null!, parent: null!,
            source: null!, license: null!, hasIssues: false, hasWiki: false,
            hasDownloads: false, hasPages: false, subscribersCount: 0, size: 0,
            allowRebaseMerge: false, allowSquashMerge: false, allowMergeCommit: true,
            archived: false, watchersCount: 0, deleteBranchOnMerge: null,
            visibility: RepositoryVisibility.Public,
            hasDiscussions: false, topics: [], allowAutoMerge: null,
            allowUpdateBranch: false, webCommitSignoffRequired: null, securityAndAnalysis: null!);

    private static PullRequestNotMergeableException NotMergeable(string message)
    {
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Body).Returns($$"""{"message":"{{message}}"}""");
        response.SetupGet(r => r.StatusCode).Returns(System.Net.HttpStatusCode.MethodNotAllowed);
        response.SetupGet(r => r.ContentType).Returns("application/json");
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        return new PullRequestNotMergeableException(response.Object);
    }
}

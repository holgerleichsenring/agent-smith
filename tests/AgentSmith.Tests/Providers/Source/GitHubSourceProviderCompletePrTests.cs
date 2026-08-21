using System.Net;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Octokit;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0490: GitHub finishes an init pull request by merging it. Branch protection is the
/// interesting case — GitHub answers a merge it will not perform with 405, and that is
/// the platform declining, not the run failing, so it comes back as a refusal carrying
/// the reason instead of an exception.
/// </summary>
public sealed class GitHubSourceProviderCompletePrTests
{
    private const string RepoUrl = "https://github.com/example/sample-server";
    private const string PrUrl = "https://github.com/example/sample-server/pull/7";
    private const string Token = "ghp-test";

    private readonly Mock<IPullRequestsClient> _pullRequests = new();

    [Fact]
    public async Task GitHubSourceProvider_CompletePullRequest_MergesIt()
    {
        _pullRequests
            .Setup(c => c.Merge("example", "sample-server", 7, It.IsAny<MergePullRequest>()))
            .ReturnsAsync(new PullRequestMerge("abc123", merged: true, "Pull Request successfully merged"));

        var completion = await CreateSut().CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Completed.Should().BeTrue();
        completion.Reason.Should().BeNull();
        _pullRequests.VerifyAll();
    }

    [Fact]
    public async Task GitHubSourceProvider_CompletePullRequest_BranchProtectionRefuses_ReportsTheReason()
    {
        _pullRequests
            .Setup(c => c.Merge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MergePullRequest>()))
            .ThrowsAsync(NotMergeable("At least 1 approving review is required by reviewers."));

        var completion = await CreateSut().CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Completed.Should().BeFalse();
        completion.Reason.Should().Contain("approving review is required");
    }

    [Fact]
    public async Task GitHubSourceProvider_CompletePullRequest_NotAPullRequestUrl_IsRefused_WithoutCallingGitHub()
    {
        var completion = await CreateSut().CompletePullRequestAsync(
            "Local repository - no PR created", new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Completed.Should().BeFalse();
        _pullRequests.Verify(
            c => c.Merge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MergePullRequest>()),
            Times.Never);
    }

    private ISourceProvider CreateSut()
    {
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.PullRequest).Returns(_pullRequests.Object);
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(Token)).Returns(client.Object);
        return new GitHubSourceProvider(
            new GitHubSourceConnection(RepoUrl, Token, "main"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);
    }

    // GitHub returns 405 with a message naming the policy that stopped the merge.
    private static PullRequestNotMergeableException NotMergeable(string message)
    {
        var response = new Mock<IResponse>();
        response.SetupGet(r => r.Body).Returns($$"""{"message":"{{message}}"}""");
        response.SetupGet(r => r.StatusCode).Returns(HttpStatusCode.MethodNotAllowed);
        response.SetupGet(r => r.ContentType).Returns("application/json");
        response.SetupGet(r => r.Headers).Returns(new Dictionary<string, string>());
        return new PullRequestNotMergeableException(response.Object);
    }
}

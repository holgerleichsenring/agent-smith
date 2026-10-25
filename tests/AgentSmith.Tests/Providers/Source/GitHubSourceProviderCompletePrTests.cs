using AgentSmith.Infrastructure.Models;
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
    // 2026-10-25-9dbbc: the repository's merge settings; merge commits allowed unless a test says otherwise.
    private Repository _repository = WithMergeSettings(mergeCommit: true, squash: true, rebase: true);

    [Fact]
    public async Task CompleteAsync_SquashOnlyRepository_MergesWithSquash()
    {
        _repository = WithMergeSettings(mergeCommit: false, squash: true, rebase: false);
        SetupMerge(PullRequestMergeMethod.Squash).ReturnsAsync(Merged());

        var completion = await Complete();

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        VerifyOnlyMergedWith(PullRequestMergeMethod.Squash);
    }

    [Fact]
    public async Task CompleteAsync_MergeCommitAllowed_MergesWithMergeCommit()
    {
        SetupMerge(PullRequestMergeMethod.Merge).ReturnsAsync(Merged());

        var completion = await Complete();

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        VerifyOnlyMergedWith(PullRequestMergeMethod.Merge);
    }

    [Fact]
    public async Task CompleteAsync_RebaseOnlyRepository_MergesWithRebase()
    {
        _repository = WithMergeSettings(mergeCommit: false, squash: false, rebase: true);
        SetupMerge(PullRequestMergeMethod.Rebase).ReturnsAsync(Merged());

        var completion = await Complete();

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        VerifyOnlyMergedWith(PullRequestMergeMethod.Rebase);
    }

    [Fact]
    public async Task CompleteAsync_SettingsUnknownAndMergeCommit405_RetriesSquash()
    {
        _repository = WithMergeSettings(mergeCommit: null, squash: null, rebase: null);
        SetupMerge(PullRequestMergeMethod.Merge).ThrowsAsync(NotMergeable("Merge commits are not allowed on this repository."));
        SetupMerge(PullRequestMergeMethod.Squash).ReturnsAsync(Merged());

        var completion = await Complete();

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        _pullRequests.Verify(c => c.Merge("example", "sample-server", 7,
            It.Is<MergePullRequest>(m => m.MergeMethod == PullRequestMergeMethod.Rebase)), Times.Never);
    }

    [Fact]
    public async Task CompleteAsync_KnownMethod405_RefusesWithoutRetrying()
    {
        _repository = WithMergeSettings(mergeCommit: true, squash: null, rebase: null);
        SetupMerge(PullRequestMergeMethod.Merge).ThrowsAsync(NotMergeable("At least 1 approving review is required by reviewers."));

        var completion = await Complete();

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        VerifyOnlyMergedWith(PullRequestMergeMethod.Merge);
    }

    [Fact]
    public async Task CompleteAsync_NoMethodAllowed_RefusesNamingTheSettings()
    {
        _repository = WithMergeSettings(mergeCommit: false, squash: false, rebase: false);

        var completion = await Complete();

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("allow_squash_merge");
        _pullRequests.Verify(c => c.Merge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MergePullRequest>()), Times.Never);
    }

    [Fact]
    public async Task GitHubSourceProvider_CompletePullRequest_MergesIt()
    {
        _pullRequests
            .Setup(c => c.Merge("example", "sample-server", 7, It.IsAny<MergePullRequest>()))
            .ReturnsAsync(new PullRequestMerge("abc123", merged: true, "Pull Request successfully merged"));

        var completion = await CreateSut().CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
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

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("approving review is required");
    }

    [Fact]
    public async Task GitHubSourceProvider_CompletePullRequest_NotAPullRequestUrl_IsRefused_WithoutCallingGitHub()
    {
        var completion = await CreateSut().CompletePullRequestAsync(
            "Local repository - no PR created", new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        _pullRequests.Verify(
            c => c.Merge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<MergePullRequest>()),
            Times.Never);
    }

    private ISourceProvider CreateSut()
    {
        var client = new Mock<IGitHubClient>();
        client.SetupGet(c => c.PullRequest).Returns(_pullRequests.Object);
        var repositories = new Mock<IRepositoriesClient>();
        repositories.Setup(r => r.Get("example", "sample-server")).ReturnsAsync(() => _repository);
        client.SetupGet(c => c.Repository).Returns(repositories.Object);
        var factory = new Mock<IGitHubClientFactory>();
        factory.Setup(f => f.Create(It.IsAny<GitHubRepoAddress>(), Token)).Returns(client.Object);
        return new GitHubSourceProvider(
            new GitHubSourceConnection(RepoUrl, Token, "main"),
            factory.Object, NullLogger<GitHubSourceProvider>.Instance);
    }

    private Task<PullRequestCompletion> Complete() =>
        CreateSut().CompletePullRequestAsync(PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

    private Moq.Language.Flow.ISetup<IPullRequestsClient, Task<PullRequestMerge>> SetupMerge(PullRequestMergeMethod method) =>
        _pullRequests.Setup(c => c.Merge("example", "sample-server", 7, It.Is<MergePullRequest>(m => m.MergeMethod == method)));

    private void VerifyOnlyMergedWith(PullRequestMergeMethod method)
    {
        _pullRequests.Verify(c => c.Merge("example", "sample-server", 7, It.Is<MergePullRequest>(m => m.MergeMethod == method)), Times.Once);
        _pullRequests.Verify(c => c.Merge(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.Is<MergePullRequest>(m => m.MergeMethod != method)), Times.Never);
    }

    private static PullRequestMerge Merged() => new("abc123", merged: true, "Pull Request successfully merged");

    private static Repository WithMergeSettings(bool? mergeCommit, bool? squash, bool? rebase) =>
        new(url: "", htmlUrl: "", cloneUrl: "", gitUrl: "", sshUrl: "", svnUrl: "",
            mirrorUrl: "", archiveUrl: "", id: 1, nodeId: "", owner: null!, name: "sample-server",
            fullName: "example/sample-server", isTemplate: false, description: "", homepage: "",
            language: "", @private: false, fork: false, forksCount: 0, stargazersCount: 0,
            defaultBranch: "main", openIssuesCount: 0, pushedAt: null,
            createdAt: default, updatedAt: default, permissions: null!, parent: null!,
            source: null!, license: null!, hasIssues: false, hasWiki: false,
            hasDownloads: false, hasPages: false, subscribersCount: 0, size: 0,
            allowRebaseMerge: rebase, allowSquashMerge: squash, allowMergeCommit: mergeCommit,
            archived: false, watchersCount: 0, deleteBranchOnMerge: null,
            visibility: RepositoryVisibility.Public,
            hasDiscussions: false, topics: [], allowAutoMerge: null,
            allowUpdateBranch: false, webCommitSignoffRequired: null, securityAndAnalysis: null!);

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

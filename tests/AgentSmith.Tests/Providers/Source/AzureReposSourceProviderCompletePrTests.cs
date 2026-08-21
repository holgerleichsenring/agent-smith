using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.Common;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace AgentSmith.Tests.Providers.Source;

/// <summary>
/// p0490: Azure DevOps finishes an init pull request by updating it to Completed
/// against the head it last merged. An unsatisfied branch policy leaves the pull
/// request Active and names itself in MergeFailureMessage — that is the reason
/// recorded for the repo, and the pull request stays open.
/// </summary>
public sealed class AzureReposSourceProviderCompletePrTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "sample-server";
    private const string Pat = "azdo-pat";
    private const string PrUrl = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7";

    [Fact]
    public async Task AzureReposSourceProvider_CompletePullRequest_CompletesIt()
    {
        var client = NewGitClientMock();
        SetupGet(client, new GitPullRequest
        {
            PullRequestId = 7,
            Status = PullRequestStatus.Active,
            LastMergeSourceCommit = new GitCommitRef { CommitId = "abc123" },
        });
        GitPullRequest? sent = null;
        SetupUpdate(client)
            .Callback<GitPullRequest, string, string, int, object, CancellationToken>(
                (update, _, _, _, _, _) => sent = update)
            .ReturnsAsync(() => new GitPullRequest { Status = PullRequestStatus.Completed });

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Completed.Should().BeTrue();
        sent!.Status.Should().Be(PullRequestStatus.Completed);
        sent.LastMergeSourceCommit!.CommitId.Should().Be("abc123",
            "completing against the head AzDO last merged is what makes a moved branch refuse");
        sent.CompletionOptions!.DeleteSourceBranch.Should().Be(false,
            "p0490 does not delete source branches");
    }

    [Fact]
    public async Task AzureReposSourceProvider_CompletePullRequest_PolicyRefuses_ReportsMergeFailureMessage()
    {
        var client = NewGitClientMock();
        SetupGet(client, new GitPullRequest { PullRequestId = 7, Status = PullRequestStatus.Active });
        SetupUpdate(client).ReturnsAsync(() => new GitPullRequest
        {
            Status = PullRequestStatus.Active,
            MergeFailureMessage = "The pull request does not satisfy the required reviewers policy.",
        });

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Completed.Should().BeFalse();
        completion.Reason.Should().Contain("required reviewers policy");
    }

    [Fact]
    public async Task AzureReposSourceProvider_CompletePullRequest_ServerThrows_IsRefused_NotRaised()
    {
        var client = NewGitClientMock();
        SetupGet(client, new GitPullRequest { PullRequestId = 7 });
        SetupUpdate(client).ThrowsAsync(new VssServiceException(
            "TF401027: You need the Git 'PullRequestBypassPolicy' permission."));

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Completed.Should().BeFalse();
        completion.Reason.Should().Contain("TF401027");
    }

    private static void SetupGet(Mock<GitHttpClient> client, GitPullRequest pr) =>
        client.Setup(c => c.GetPullRequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => pr);

    private static Moq.Language.Flow.ISetup<GitHttpClient, Task<GitPullRequest>> SetupUpdate(
        Mock<GitHttpClient> client) =>
        client.Setup(c => c.UpdatePullRequestAsync(
            It.IsAny<GitPullRequest>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
            It.IsAny<object>(), It.IsAny<CancellationToken>()));

    private static ISourceProvider CreateSut(GitHttpClient gitClient)
    {
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>())).Returns(gitClient);
        factory.Setup(f => f.CreateGitClientAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gitClient);
        return new AzureReposSourceProvider(
            new AzureReposSourceConnection(OrgUrl, Project, Repo, Pat, "main"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);
    }

    // GitHttpClient is a concrete class; Moq needs ctor args even though every call
    // site goes through a Setup'd virtual method.
    private static Mock<GitHttpClient> NewGitClientMock() =>
        new(new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
}

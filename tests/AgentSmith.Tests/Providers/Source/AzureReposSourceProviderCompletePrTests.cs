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
/// p0501: p0490 finished an Azure Repos pull request by updating it to Completed — an
/// immediate merge, which Azure DevOps refuses outright whenever a branch policy
/// requires an integration build. That is the operator's setup, so auto-completion was
/// unavailable in exactly the environment it was built for.
/// <para>
/// The mechanism that works is APPROVE then ARM: an approving reviewer vote plus
/// AutoCompleteSetBy, after which the pull request merges itself when the build goes
/// green. These pin all three outcomes apart — merged, armed, refused — because the
/// operator has to be able to tell "it is handling itself" from "it needs you".
/// </para>
/// </summary>
public sealed class AzureReposSourceProviderCompletePrTests
{
    private const string OrgUrl = "https://dev.azure.com/example";
    private const string Project = "demo";
    private const string Repo = "sample-server";
    private const string Pat = "azdo-pat";
    private const string PrUrl = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7";

    private static readonly IdentityRef Creator = new() { Id = "id-42", DisplayName = "agent-smith" };

    [Fact]
    public async Task AzureRepos_CompleteAsync_ApprovesAndArmsAutoComplete()
    {
        var client = NewGitClientMock();
        SetupGet(client, Active());
        IdentityRefWithVote? vote = null;
        SetupReviewer(client)
            .Callback<IdentityRefWithVote, string, string, int, string, object, CancellationToken>(
                (v, _, _, _, _, _, _) => vote = v)
            .ReturnsAsync(() => new IdentityRefWithVote());
        GitPullRequest? sent = null;
        SetupUpdate(client)
            .Callback<GitPullRequest, string, string, int, object, CancellationToken>(
                (update, _, _, _, _, _) => sent = update)
            .ReturnsAsync(() => Armed());

        await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        vote!.Vote.Should().Be(10, "10 is Azure DevOps' approving vote");
        sent!.AutoCompleteSetBy!.Id.Should().Be(
            Creator.Id, "our own run opened this PR, so CreatedBy IS the token's identity");
        sent.Status.Should().Be(
            PullRequestStatus.NotSet, "asking for an immediate merge is what a policy refuses");
        sent.CompletionOptions!.DeleteSourceBranch.Should().Be(false, "p0490 keeps the init branch");
    }

    [Fact]
    public async Task AzureRepos_PolicyPending_ReportsArmed_NotRefused()
    {
        var client = NewGitClientMock();
        SetupGet(client, Active());
        SetupReviewer(client).ReturnsAsync(() => new IdentityRefWithVote());
        SetupUpdate(client).ReturnsAsync(() => Armed(
            "The pull request is queued for auto-complete when the build policy passes."));

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Armed);
        completion.Settled.Should().BeTrue("nobody has to come back to an armed pull request");
        completion.Reason.Should().Contain("build policy");
    }

    [Fact]
    public async Task AzureRepos_CompletedImmediately_ReportsMerged()
    {
        var client = NewGitClientMock();
        SetupGet(client, Active());
        SetupReviewer(client).ReturnsAsync(() => new IdentityRefWithVote());
        SetupUpdate(client).ReturnsAsync(() => new GitPullRequest { Status = PullRequestStatus.Completed });

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
    }

    [Fact]
    public async Task AzureRepos_ArmingRejected_ReportsRefusedWithThePlatformsReason()
    {
        var client = NewGitClientMock();
        SetupGet(client, Active());
        SetupReviewer(client).ReturnsAsync(() => new IdentityRefWithVote());
        SetupUpdate(client).ReturnsAsync(() => new GitPullRequest
        {
            Status = PullRequestStatus.Active,
            MergeFailureMessage = "The pull request does not satisfy the required reviewers policy.",
        });

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("required reviewers policy");
    }

    [Fact]
    public async Task AzureRepos_ServerThrows_IsRefused_NotRaised()
    {
        var client = NewGitClientMock();
        SetupGet(client, Active());
        SetupReviewer(client).ReturnsAsync(() => new IdentityRefWithVote());
        SetupUpdate(client).ThrowsAsync(new VssServiceException(
            "TF401027: You need the Git 'PullRequestBypassPolicy' permission."));

        var completion = await CreateSut(client.Object).CompletePullRequestAsync(
            PrUrl, new BranchName("agentsmith/init"), CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("TF401027");
    }

    private static GitPullRequest Active() => new()
    {
        PullRequestId = 7,
        Status = PullRequestStatus.Active,
        CreatedBy = Creator,
        LastMergeSourceCommit = new GitCommitRef { CommitId = "abc123" },
    };

    private static GitPullRequest Armed(string? mergeFailureMessage = null) => new()
    {
        Status = PullRequestStatus.Active,
        AutoCompleteSetBy = Creator,
        MergeFailureMessage = mergeFailureMessage,
    };

    private static void SetupGet(Mock<GitHttpClient> client, GitPullRequest pr) =>
        client.Setup(c => c.GetPullRequestAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(),
                It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => pr);

    private static Moq.Language.Flow.ISetup<GitHttpClient, Task<IdentityRefWithVote>> SetupReviewer(
        Mock<GitHttpClient> client) =>
        client.Setup(c => c.CreatePullRequestReviewerAsync(
            It.IsAny<IdentityRefWithVote>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()));

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

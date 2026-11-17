using AgentSmith.Contracts.Models.Configuration;
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
/// 2026-11-17-023fa: a code run's merge on Azure Repos. Bypass completes now past the policies — Status Completed,
/// the last merge source commit, BypassPolicy with a reason — and a missing permission is a refusal that names the
/// permission and the key, never a silent fall back to arming. Respect arms auto-complete exactly as init does.
/// </summary>
public sealed class AzureReposMergeModeTests
{
    private const string PrUrl = "https://dev.azure.com/example/demo/_git/sample-server/pullrequest/7";
    private static readonly BranchName Branch = new("agent-smith/42");
    private static readonly IdentityRef Creator = new() { Id = "id-42", DisplayName = "agent-smith" };

    [Fact]
    public async Task Bypass_CompletesNow_PastThePolicies()
    {
        var client = NewClient();
        GitPullRequest? sent = null;
        Update(client).Callback<GitPullRequest, string, string, int, object, CancellationToken>(
                (u, _, _, _, _, _) => sent = u)
            .ReturnsAsync(() => new GitPullRequest { Status = PullRequestStatus.Completed });

        var completion = await Sut(client.Object).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Merged);
        sent!.Status.Should().Be(PullRequestStatus.Completed);
        sent.LastMergeSourceCommit!.CommitId.Should().Be("abc123", "Azure completes only against the commit it last merged");
        sent.CompletionOptions!.BypassPolicy.Should().BeTrue();
        sent.CompletionOptions.BypassReason.Should().Contain("agent-smith");
        client.Verify(c => c.CreatePullRequestReviewerAsync(It.IsAny<IdentityRefWithVote>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()),
            Times.Never, "a bypass needs no approving vote");
    }

    [Fact]
    public async Task Bypass_QueuedMerge_IsArmed()
    {
        var client = NewClient();
        Update(client).ReturnsAsync(() => new GitPullRequest
        {
            Status = PullRequestStatus.Active, MergeStatus = PullRequestAsyncStatus.Queued,
        });

        var completion = await Sut(client.Object).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Armed, "Azure merges asynchronously");
    }

    [Fact]
    public async Task Bypass_Conflicts_IsRefused()
    {
        var client = NewClient();
        Update(client).ReturnsAsync(() => new GitPullRequest
        {
            Status = PullRequestStatus.Active, MergeStatus = PullRequestAsyncStatus.Conflicts,
        });

        var completion = await Sut(client.Object).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().Contain("conflicts");
    }

    [Fact]
    public async Task Bypass_WithoutPermission_NamesThePermissionAndTheKey()
    {
        var client = NewClient();
        Update(client).ThrowsAsync(new VssServiceException(
            "TF401027: You need the Git 'PullRequestBypassPolicy' permission to perform this action."));

        var completion = await Sut(client.Object).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Bypass, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Refused);
        completion.Reason.Should().StartWith("cannot bypass branch policies on Azure Repos: TF401027");
        completion.Reason.Should().Contain("Bypass policies when completing pull requests");
        completion.Reason.Should().EndWith("or set respect_branch_protection: true");
    }

    [Fact]
    public async Task Respect_ApprovesAndArms_AsInitDoes()
    {
        var client = NewClient();
        client.Setup(c => c.CreatePullRequestReviewerAsync(It.IsAny<IdentityRefWithVote>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<int>(), It.IsAny<string>(), It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new IdentityRefWithVote());
        GitPullRequest? sent = null;
        Update(client).Callback<GitPullRequest, string, string, int, object, CancellationToken>(
                (u, _, _, _, _, _) => sent = u)
            .ReturnsAsync(() => new GitPullRequest { Status = PullRequestStatus.Active, AutoCompleteSetBy = Creator });

        var completion = await Sut(client.Object).MergePullRequestAsync(PrUrl, Branch, BranchProtection.Respect, CancellationToken.None);

        completion.Outcome.Should().Be(PullRequestCompletionOutcome.Armed);
        sent!.AutoCompleteSetBy!.Id.Should().Be(Creator.Id);
        sent.CompletionOptions!.BypassPolicy.Should().NotBe(true);
    }

    private static Mock<GitHttpClient> NewClient()
    {
        var client = new Mock<GitHttpClient>(new Uri("https://localhost/fake"),
            new VssCredentials(new VssBasicCredential(string.Empty, "fake")));
        client.Setup(c => c.GetPullRequestAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(),
                It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<int?>(), It.IsAny<bool?>(), It.IsAny<bool?>(),
                It.IsAny<object>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => new GitPullRequest
            {
                PullRequestId = 7, Status = PullRequestStatus.Active, CreatedBy = Creator,
                LastMergeSourceCommit = new GitCommitRef { CommitId = "abc123" },
            });
        return client;
    }

    private static Moq.Language.Flow.ISetup<GitHttpClient, Task<GitPullRequest>> Update(Mock<GitHttpClient> client) =>
        client.Setup(c => c.UpdatePullRequestAsync(It.IsAny<GitPullRequest>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<int>(), It.IsAny<object>(), It.IsAny<CancellationToken>()));

    private static ISourceProvider Sut(GitHttpClient gitClient)
    {
        var factory = new Mock<IAzDoClientFactory>();
        factory.Setup(f => f.CreateGitClient(It.IsAny<string>(), It.IsAny<string>())).Returns(gitClient);
        factory.Setup(f => f.CreateGitClientAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(gitClient);
        return new AzureReposSourceProvider(
            new AzureReposSourceConnection("https://dev.azure.com/example", "demo", "sample-server", "pat", "main"),
            factory.Object, NullLogger<AzureReposSourceProvider>.Instance);
    }
}

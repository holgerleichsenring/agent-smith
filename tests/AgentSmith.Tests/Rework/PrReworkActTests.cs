using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Providers.Source;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TeamFoundation.SourceControl.WebApi;
using Microsoft.VisualStudio.Services.WebApi;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9c: the run reads its pull-request act from the pull request itself, with
/// the same trust the webhook applies; Azure DevOps votes count only while they stand.</summary>
public sealed class PrReworkActTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 8, 9, 0, 0, TimeSpan.Zero);

    private static async Task<PipelineContext> RunWith(PrReviewNote request, bool trusted)
    {
        var provider = new Mock<ISourceProvider>();
        provider.As<IPrReviewActReader>().Setup(a => a.ChangesRequestedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([request]);
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(provider.Object);
        var trust = new Mock<IPrReviewAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(trusted);
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.Repos, (IReadOnlyList<RepoConnection>)[new RepoConnection { Name = "api", Type = RepoType.GitHub }]);
        pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("run-1", "success", Start, true)
        {
            PullRequestUrls = new Dictionary<string, string> { ["api"] = "https://github.com/o/api/pull/4" },
        });
        await new PrReviewFeedbackFetcher(sources.Object, trust.Object, NullLogger<PrReviewFeedbackFetcher>.Instance)
            .FetchAsync(pipeline, CancellationToken.None);
        return pipeline;
    }

    private static PrReviewNote Request(int minutes) =>
        new(new PrCommentAuthor("u", "o/api", "alice", "alice"), false, false, Start.AddMinutes(minutes), string.Empty);

    [Fact]
    public async Task ReworkActReader_ChangesRequestedAfterAttempt_SetsPullRequestAct()
    {
        var pipeline = await RunWith(Request(30), trusted: true);

        pipeline.TryGet<ReworkAct>(ContextKeys.ReworkAct, out var act).Should().BeTrue();
        act.Should().Be(new ReworkAct("alice", Start.AddMinutes(30), ReworkChannel.PullRequest));
    }

    [Fact]
    public async Task ReworkActReader_UntrustedChangesRequested_SetsNothing() =>
        (await RunWith(Request(30), trusted: false)).Has(ContextKeys.ReworkAct).Should().BeFalse();

    [Fact]
    public async Task ReworkActReader_ChangesRequestedBeforeAttempt_SetsNothing() =>
        (await RunWith(Request(-30), trusted: true)).Has(ContextKeys.ReworkAct).Should().BeFalse();

    private static GitPullRequestCommentThread VoteThread(string voter, DateTime at) => new()
    {
        PublishedDate = at,
        Properties = new PropertiesCollection { ["CodeReviewThreadType"] = "VoteUpdate", ["CodeReviewVoteResult"] = "-5" },
        Comments = [new Comment { Author = new IdentityRef { Id = voter, UniqueName = voter }, CommentType = CommentType.System }],
    };

    [Fact]
    public void AzureVotes_VoterStillAtMinusFive_NewestThreadCounts()
    {
        var older = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);
        var standing = AzureReposVoteActs.Standing([VoteThread("v", older), VoteThread("v", older.AddHours(1))],
            [new IdentityRefWithVote { Id = "v", Vote = -5 }], "creator", "u", "r", "p");

        standing.Single().At.Should().Be(new DateTimeOffset(older.AddHours(1)));
    }

    [Fact]
    public void AzureVotes_VoteResetOrCreator_NoAct()
    {
        var at = new DateTime(2026, 10, 8, 10, 0, 0, DateTimeKind.Utc);

        AzureReposVoteActs.Standing([VoteThread("v", at)], [new IdentityRefWithVote { Id = "v", Vote = 10 }], "creator", "u", "r", "p")
            .Should().BeEmpty();
        AzureReposVoteActs.Standing([VoteThread("creator", at)], [new IdentityRefWithVote { Id = "creator", Vote = -5 }], "creator", "u", "r", "p")
            .Should().BeEmpty();
    }
}

using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9c: a Request changes on GitHub and Azure DevOps, through the admission.</summary>
public sealed class PrReworkAdmissionTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkEntry> _rework = new();
    private readonly Mock<IPrReviewAuthorTrust> _trust = new();
    private readonly Mock<IPrCommentProvider> _comments = new();
    private readonly Mock<IPrReviewActReader> _acts;

    public PrReworkAdmissionTests()
    {
        _attempts.Setup(a => a.LatestAsync("app", "12", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", At.AddHours(-1), true));
        _trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.Is<PrCommentAuthor>(a => a.AuthorId != "mallory"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _rework.Setup(r => r.EnterAsync(It.IsAny<ResolvedProject>(), "12", It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("run-2"));
        _acts = _comments.As<IPrReviewActReader>();
    }

    private static AgentSmithConfig Config() => new()
    {
        Projects = new Dictionary<string, ResolvedProject>
        {
            ["app"] = new() { Name = "app", Repos = [new RepoConnection { Name = "app", Url = "https://github.com/o/app", Type = RepoType.GitHub }] },
            ["app-api"] = new() { Name = "app-api", Repos = [new RepoConnection { Name = "api", Url = "https://github.com/o/app-api", Type = RepoType.GitHub }] },
            ["ado"] = new() { Name = "ado", Repos = [new RepoConnection { Name = "r", Url = "https://dev.azure.com/org/proj/_git/r", Type = RepoType.AzureDevOps }] },
        },
    };

    private PrReworkAdmission Admission()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config());
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_comments.As<ISourceProvider>().Object);
        return new PrReworkAdmission(loader.Object, new ServerContext("c.yml"), new ConfiguredRepoFinder(), _attempts.Object,
            _trust.Object, _rework.Object, sources.Object, NullLogger<PrReworkAdmission>.Instance);
    }

    private GitHubPrReviewWebhookHandler GitHub() => new(Admission(), NullLogger<GitHubPrReviewWebhookHandler>.Instance);

    private static string Review(string state = "changes_requested", string reviewer = "alice", string headRef = "agent-smith/12",
        string headRepo = "o/app", string repo = "app", string prAuthor = "bot") => $$"""
        { "action": "submitted",
          "review": { "state": "{{state}}", "user": { "login": "{{reviewer}}" }, "author_association": "MEMBER", "submitted_at": "2026-10-08T12:00:00Z" },
          "pull_request": { "number": 4, "user": { "login": "{{prAuthor}}" }, "head": { "ref": "{{headRef}}", "repo": { "full_name": "{{headRepo}}" } } },
          "repository": { "full_name": "o/{{repo}}", "html_url": "https://github.com/o/{{repo}}", "clone_url": "https://github.com/o/{{repo}}.git" } }
        """;

    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task GitHubPrReview_ChangesRequestedOnOurBranch_StartsReworkAndComments()
    {
        (await GitHub().HandleAsync(Review(), NoHeaders)).Handled.Should().BeTrue();

        _rework.Verify(r => r.EnterAsync(It.Is<ResolvedProject>(p => p.Name == "app"), "12",
            It.Is<ReworkAct>(a => a.Channel == ReworkChannel.PullRequest && a.At == At), "code", It.IsAny<CancellationToken>()), Times.Once);
        _comments.Verify(c => c.PostCommentAsync("4", It.Is<string>(t => t.Contains("run-2")), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GitHubPrReview_Approved_NotHandled() =>
        (await GitHub().HandleAsync(Review(state: "approved"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task GitHubPrReview_ByPrAuthor_NotHandled() =>
        (await GitHub().HandleAsync(Review(reviewer: "bot", prAuthor: "bot"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task GitHubPrReview_ForkHead_NotHandled() =>
        (await GitHub().HandleAsync(Review(headRepo: "stranger/app"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task GitHubPrReview_InitBranch_NotHandled() =>
        (await GitHub().HandleAsync(Review(headRef: "agent-smith/init"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task GitHubPrReview_UntrustedReviewer_NotHandled() =>
        (await GitHub().HandleAsync(Review(reviewer: "mallory"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task PrRework_PrefixSiblingRepo_DoesNotMatch()
    {
        // o/app-api would contain o/app as a substring; equality keeps the review on app-api, which has no run.
        (await GitHub().HandleAsync(Review(repo: "app-api", headRepo: "o/app-api"), NoHeaders)).Handled.Should().BeFalse();
        _rework.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrRework_NoRunForTicket_NotHandled() =>
        (await GitHub().HandleAsync(Review(headRef: "agent-smith/99"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task PrRework_LiveRun_CommentsWhenToRetry()
    {
        _rework.Setup(r => r.EnterAsync(It.IsAny<ResolvedProject>(), "12", It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.WorkedOn("run-live"));

        await GitHub().HandleAsync(Review(), NoHeaders);

        _comments.Verify(c => c.PostCommentAsync("4", It.Is<string>(t => t.Contains("run-live") && t.Contains("submit Request changes again")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ---- Azure DevOps ----

    private static string Vote(int vote, string voter = "v-1") => $$"""
        { "eventType": "git.pullrequest.updated", "publisherId": "tfs",
          "resource": { "pullRequestId": 9, "status": "active", "sourceRefName": "refs/heads/agent-smith/12",
            "createdBy": { "id": "creator" },
            "repository": { "id": "r-id", "name": "r", "remoteUrl": "https://org@dev.azure.com/org/proj/_git/r" },
            "reviewers": [ { "id": "{{voter}}", "vote": {{vote}} } ] } }
        """;

    private static readonly Dictionary<string, string> VoteHeader =
        new(StringComparer.OrdinalIgnoreCase) { ["X-AgentSmith-Change"] = "review-vote" };

    private AzureDevOpsPrReviewVoteWebhookHandler Azure()
    {
        _attempts.Setup(a => a.LatestAsync("ado", "12", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", At.AddHours(-1), true));
        _rework.Setup(r => r.EnterAsync(It.Is<ResolvedProject>(p => p.Name == "ado"), "12", It.IsAny<ReworkAct>(), "code", It.IsAny<CancellationToken>()))
            .ReturnsAsync(ReworkOutcome.Started("run-3"));
        _acts.Setup(a => a.ChangesRequestedAsync("https://dev.azure.com/org/proj/_git/r/pullrequest/9", It.IsAny<CancellationToken>()))
            .ReturnsAsync([new PrReviewNote(new PrCommentAuthor("u", "r-id", "v-1", "voter@x") { ProjectId = "p" }, false, false, At, string.Empty)]);
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(Config());
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_comments.As<ISourceProvider>().Object);
        return new AzureDevOpsPrReviewVoteWebhookHandler(Admission(), new ConfiguredRepoFinder(), loader.Object,
            new ServerContext("c.yml"), sources.Object, NullLogger<AzureDevOpsPrReviewVoteWebhookHandler>.Instance);
    }

    [Fact]
    public async Task AzureDevOpsPr_VoteHeaderVoterStillMinusFive_StartsRework()
    {
        (await Azure().HandleAsync(Vote(-5), VoteHeader)).Handled.Should().BeTrue();

        _rework.Verify(r => r.EnterAsync(It.Is<ResolvedProject>(p => p.Name == "ado"), "12",
            It.Is<ReworkAct>(a => a.At == At && a.Channel == ReworkChannel.PullRequest), "code", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AzureDevOpsPr_VoteHeaderVoterReset_NotHandled() =>
        (await Azure().HandleAsync(Vote(10), VoteHeader)).Handled.Should().BeFalse();

    [Fact]
    public async Task AzureDevOpsPr_VoteHeaderUntrustedVoter_NoPrReview()
    {
        _trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var prReview = new AzureDevOpsPrEventWebhookHandler(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"),
            new PrReviewRouteResolver(new ConfiguredRepoFinder()), NullLogger<AzureDevOpsPrEventWebhookHandler>.Instance);

        (await Azure().HandleAsync(Vote(-5), VoteHeader)).Handled.Should().BeFalse();
        (await prReview.HandleAsync(Vote(-5), VoteHeader)).Handled.Should().BeFalse();
    }

    [Fact]
    public async Task AzureDevOpsPr_NoHeader_KeepsPrReview() =>
        (await Azure().HandleAsync(Vote(-5), NoHeaders)).Handled.Should().BeFalse();
}

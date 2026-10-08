using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>
/// 2026-10-08-e8b9c: a Request changes on GitHub and Azure DevOps, through the admission.
/// 2026-10-08-0781: an admitted review nudges the ticket; nothing is read from the host in the request.
/// </summary>
public sealed class PrReworkAdmissionTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkNudges> _nudges = new();

    public PrReworkAdmissionTests()
    {
        _attempts.Setup(a => a.LatestAsync(It.IsIn("app", "ado"), "12", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", At.AddHours(-1), true));
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

    internal static PrReworkAdmission Admission(IPreviousAttemptReader attempts, IReworkNudges nudges, AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return new PrReworkAdmission(loader.Object, new ServerContext("c.yml"), new ConfiguredRepoFinder(), attempts,
            nudges, NullLogger<PrReworkAdmission>.Instance);
    }

    private GitHubPrReviewWebhookHandler GitHub() =>
        new(Admission(_attempts.Object, _nudges.Object, Config()), NullLogger<GitHubPrReviewWebhookHandler>.Instance);

    private static string Review(string state = "changes_requested", string reviewer = "alice", string headRef = "agent-smith/12",
        string headRepo = "o/app", string repo = "app", string prAuthor = "bot") => $$"""
        { "action": "submitted",
          "review": { "state": "{{state}}", "user": { "login": "{{reviewer}}" }, "author_association": "MEMBER", "submitted_at": "2026-10-08T12:00:00Z" },
          "pull_request": { "number": 4, "html_url": "https://github.com/o/{{repo}}/pull/4", "user": { "login": "{{prAuthor}}" },
            "head": { "ref": "{{headRef}}", "repo": { "full_name": "{{headRepo}}" } } },
          "repository": { "full_name": "o/{{repo}}", "html_url": "https://github.com/o/{{repo}}", "clone_url": "https://github.com/o/{{repo}}.git" } }
        """;

    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.OrdinalIgnoreCase);

    private void VerifyNudged(string project, string prUrl) =>
        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.Project == project && r.TicketId == "12"
            && r.Origin == ReworkNudgeOrigin.PullRequest && r.PrUrl == prUrl && r.Channel == ReworkChannel.PullRequest),
            It.IsAny<CancellationToken>()), Times.Once);

    [Fact]
    public async Task GitHubPrReview_ChangesRequestedOnOurBranch_NudgesTheTicket()
    {
        (await GitHub().HandleAsync(Review(), NoHeaders)).Handled.Should().BeTrue();

        VerifyNudged("app", "https://github.com/o/app/pull/4");
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
    public async Task PrRework_PrefixSiblingRepo_DoesNotMatch()
    {
        // o/app-api would contain o/app as a substring; equality keeps the review on app-api, which has no run.
        (await GitHub().HandleAsync(Review(repo: "app-api", headRepo: "o/app-api"), NoHeaders)).Handled.Should().BeFalse();
        _nudges.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task PrRework_NoRunForTicket_NotHandled() =>
        (await GitHub().HandleAsync(Review(headRef: "agent-smith/99"), NoHeaders)).Handled.Should().BeFalse();

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

    private AzureDevOpsPrReviewVoteWebhookHandler Azure() =>
        new(Admission(_attempts.Object, _nudges.Object, Config()), NullLogger<AzureDevOpsPrReviewVoteWebhookHandler>.Instance);

    [Fact]
    public async Task AzureDevOpsPr_VoteHeaderMinusFive_NudgesTheTicket()
    {
        (await Azure().HandleAsync(Vote(-5), VoteHeader)).Handled.Should().BeTrue();

        VerifyNudged("ado", "https://dev.azure.com/org/proj/_git/r/pullrequest/9");
    }

    [Fact]
    public async Task AzureDevOpsPr_VoteHeaderVoterReset_NotHandled() =>
        (await Azure().HandleAsync(Vote(10), VoteHeader)).Handled.Should().BeFalse();

    [Fact]
    public async Task AzureDevOpsPr_CreatorsOwnVote_NotHandled() =>
        (await Azure().HandleAsync(Vote(-5, voter: "creator"), VoteHeader)).Handled.Should().BeFalse();

    [Fact]
    public async Task AzureDevOpsPr_VoteHeader_NeverStartsPrReview()
    {
        var prReview = new AzureDevOpsPrEventWebhookHandler(Mock.Of<IConfigurationLoader>(), new ServerContext("c.yml"),
            new PrReviewRouteResolver(new ConfiguredRepoFinder()), NullLogger<AzureDevOpsPrEventWebhookHandler>.Instance);

        (await prReview.HandleAsync(Vote(-5), VoteHeader)).Handled.Should().BeFalse();
    }

    [Fact]
    public async Task AzureDevOpsPr_NoHeader_KeepsPrReview() =>
        (await Azure().HandleAsync(Vote(-5), NoHeaders)).Handled.Should().BeFalse();
}

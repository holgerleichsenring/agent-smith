using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Services.Webhooks;
using AgentSmith.Tests.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework.Triggers;

/// <summary>
/// A review, a vote, a merge, a close and a reopen delivered by a webhook only refresh the pull
/// request's record — a Refresh nudge that is not late; a Request changes keeps its act.
/// </summary>
public sealed class PrReviewTriggerTests
{
    private const string GitHubPr = "https://github.com/o/app/pull/4";
    private const string GitLabMr = "https://gitlab.example/o/app/-/merge_requests/4";
    private const string AzurePr = "https://dev.azure.com/org/proj/_git/r/pullrequest/9";
    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> VoteHeader = new(StringComparer.OrdinalIgnoreCase) { ["X-AgentSmith-Change"] = "review-vote" };
    private readonly Mock<IPreviousAttemptReader> _attempts = new();
    private readonly Mock<IReworkNudges> _nudges = new();
    private readonly List<ReworkNudgeRequest> _nudged = [];

    public PrReviewTriggerTests()
    {
        _attempts.Setup(a => a.LatestAsync(It.IsAny<string>(), "12", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", DateTimeOffset.UnixEpoch, true));
        _nudges.Setup(n => n.EnqueueAsync(It.IsAny<ReworkNudgeRequest>(), It.IsAny<CancellationToken>()))
            .Callback<ReworkNudgeRequest, CancellationToken>((r, _) => _nudged.Add(r)).Returns(Task.CompletedTask);
    }

    private static AgentSmithConfig Config(bool polls = false)
    {
        var tracker = new TrackerConnection { Name = "t", Polling = new PollingConfig { Enabled = polls } };
        return new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["gh"] = new() { Name = "gh", Tracker = tracker, Repos = [new RepoConnection { Name = "app", Url = "https://github.com/o/app", Type = RepoType.GitHub }] },
                ["gl"] = new() { Name = "gl", Tracker = tracker, Repos = [new RepoConnection { Name = "app", Url = "https://gitlab.example/o/app", Type = RepoType.GitLab }] },
                ["ado"] = new() { Name = "ado", Tracker = tracker, Repos = [new RepoConnection { Name = "r", Url = "https://dev.azure.com/org/proj/_git/r", Type = RepoType.AzureDevOps }] },
            },
            Trackers = new Dictionary<string, TrackerConnection> { ["t"] = tracker },
        };
    }

    private PrRecordNudge Records(AgentSmithConfig config) => PrRecordNudges.Of(PrReworkAdmissionTests.Owner(_attempts.Object, config), _nudges.Object);

    private PrReworkAdmission Admission(AgentSmithConfig config) => PrReworkAdmissionTests.Admission(_attempts.Object, _nudges.Object, config);

    private static TriggerModeGate Gate(AgentSmithConfig config)
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(config);
        return new TriggerModeGate(loader.Object, new ServerContext("c.yml"), new ConfiguredRepoFinder());
    }

    private void ShouldBeOneRefresh(string project, string url)
    {
        _nudged.Should().ContainSingle();
        _nudged[0].Should().Match<ReworkNudgeRequest>(r => r.Project == project && r.TicketId == "12" && r.PrUrl == url
            && r.Origin == ReworkNudgeOrigin.Refresh && r.FromDelivery && !r.IsLate);
    }

    private GitHubPrReviewWebhookHandler GitHubReviews() =>
        new(Admission(Config()), Records(Config()), NullLogger<GitHubPrReviewWebhookHandler>.Instance);

    private AzureDevOpsPrReviewVoteWebhookHandler AzureVotes() =>
        new(Admission(Config()), Records(Config()), NullLogger<AzureDevOpsPrReviewVoteWebhookHandler>.Instance);

    [Fact]
    public async Task GitHubPrReview_Approved_NudgesRefreshAndStartsNothing()
    {
        (await GitHubReviews().HandleAsync(Review("approved"), NoHeaders)).Handled.Should().BeTrue();

        ShouldBeOneRefresh("gh", GitHubPr);
    }

    [Fact]
    public async Task GitHubPrReview_ChangesRequested_KeepsPullRequestOrigin()
    {
        (await GitHubReviews().HandleAsync(Review("changes_requested"), NoHeaders)).Handled.Should().BeTrue();

        _nudged.Should().ContainSingle().Which.Origin.Should().Be(ReworkNudgeOrigin.PullRequest);
    }

    [Fact]
    public async Task AzureDevOpsVote_OtherReviewerApproval_NudgesRefresh()
    {
        (await AzureVotes().HandleAsync(Vote(10), VoteHeader)).Handled.Should().BeTrue();

        ShouldBeOneRefresh("ado", AzurePr);
    }

    [Fact]
    public async Task AzureDevOpsVote_ResetToZero_NudgesRefresh()
    {
        (await AzureVotes().HandleAsync(Vote(0), VoteHeader)).Handled.Should().BeTrue();

        ShouldBeOneRefresh("ado", AzurePr);
    }

    [Fact]
    public async Task PrClosedOrMergedDelivery_EveryHost_NudgesRefreshAndStartsNothing()
    {
        var config = Config();
        var github = new GitHubPrStateWebhookHandler(Records(config), NullLogger<GitHubPrStateWebhookHandler>.Instance);
        var gitlab = new GitLabMrStateWebhookHandler(Records(config), NullLogger<GitLabMrStateWebhookHandler>.Instance);
        var azure = new AzureDevOpsPrStateWebhookHandler(Records(config), NullLogger<AzureDevOpsPrStateWebhookHandler>.Instance);

        (await github.HandleAsync(GitHubState("closed"), NoHeaders)).Handled.Should().BeTrue();
        (await github.HandleAsync(GitHubState("reopened"), NoHeaders)).Handled.Should().BeTrue();
        (await github.HandleAsync(GitHubState("synchronize"), NoHeaders)).Handled.Should().BeFalse();
        (await gitlab.HandleAsync(GitLabState("merge"), NoHeaders)).Handled.Should().BeTrue();
        (await gitlab.HandleAsync(GitLabState("approved"), NoHeaders)).Handled.Should().BeTrue();
        (await gitlab.HandleAsync(GitLabState("open"), NoHeaders)).Handled.Should().BeFalse();
        (await azure.HandleAsync(AzureState("git.pullrequest.merged", "active"), NoHeaders)).Handled.Should().BeTrue();
        (await azure.HandleAsync(AzureState("git.pullrequest.updated", "abandoned"), NoHeaders)).Handled.Should().BeTrue();
        (await azure.HandleAsync(AzureState("git.pullrequest.updated", "active"), NoHeaders)).Handled.Should().BeFalse();

        _nudged.Select(n => n.PrUrl).Should().Equal(GitHubPr, GitHubPr, GitLabMr, GitLabMr, AzurePr, AzurePr);
        _nudged.Should().OnlyContain(n => n.Origin == ReworkNudgeOrigin.Refresh && n.FromDelivery && n.TicketId == "12");
    }

    [Fact]
    public async Task PrStateDelivery_PollingProjectRepo_IsRefused()
    {
        var config = Config(polls: true);
        var github = new GitHubPrStateWebhookHandler(Records(config), NullLogger<GitHubPrStateWebhookHandler>.Instance, Gate(config));
        var gitlab = new GitLabMrStateWebhookHandler(Records(config), NullLogger<GitLabMrStateWebhookHandler>.Instance, Gate(config));
        var azure = new AzureDevOpsPrStateWebhookHandler(Records(config), NullLogger<AzureDevOpsPrStateWebhookHandler>.Instance, Gate(config));

        (await github.HandleAsync(GitHubState("closed"), NoHeaders)).SkipReason.Should().Contain("polls");
        (await gitlab.HandleAsync(GitLabState("merge"), NoHeaders)).SkipReason.Should().Contain("polls");
        (await azure.HandleAsync(AzureState("git.pullrequest.merged", "completed"), NoHeaders)).SkipReason.Should().Contain("polls");
        _nudged.Should().BeEmpty();
    }

    private static string Review(string state) => $$"""
        { "action": "submitted",
          "review": { "state": "{{state}}", "user": { "login": "alice" }, "author_association": "MEMBER" },
          "pull_request": { "number": 4, "html_url": "{{GitHubPr}}", "user": { "login": "bot" },
            "head": { "ref": "agent-smith/12", "repo": { "full_name": "o/app" } } },
          "repository": { "full_name": "o/app", "html_url": "https://github.com/o/app", "clone_url": "https://github.com/o/app.git" } }
        """;

    private static string Vote(int vote) => $$"""
        { "eventType": "git.pullrequest.updated",
          "resource": { "pullRequestId": 9, "status": "active", "sourceRefName": "refs/heads/agent-smith/12", "createdBy": { "id": "creator" },
            "repository": { "id": "r-id", "name": "r", "remoteUrl": "https://org@dev.azure.com/org/proj/_git/r" },
            "reviewers": [ { "id": "v-1", "vote": {{vote}} } ] } }
        """;

    private static string GitHubState(string action) => $$"""
        { "action": "{{action}}",
          "pull_request": { "number": 4, "html_url": "{{GitHubPr}}", "merged": true,
            "head": { "ref": "agent-smith/12", "repo": { "full_name": "o/app" } } },
          "repository": { "full_name": "o/app", "html_url": "https://github.com/o/app", "clone_url": "https://github.com/o/app.git" } }
        """;

    private static string GitLabState(string action) => $$"""
        { "object_kind": "merge_request", "project": { "id": 5, "web_url": "https://gitlab.example/o/app" },
          "object_attributes": { "action": "{{action}}", "iid": 4, "url": "{{GitLabMr}}", "source_branch": "agent-smith/12",
            "source_project_id": 5, "target_project_id": 5 } }
        """;

    private static string AzureState(string eventType, string status) => $$"""
        { "eventType": "{{eventType}}",
          "resource": { "pullRequestId": 9, "status": "{{status}}", "sourceRefName": "refs/heads/agent-smith/12",
            "repository": { "id": "r-id", "name": "r", "remoteUrl": "https://org@dev.azure.com/org/proj/_git/r" } } }
        """;
}

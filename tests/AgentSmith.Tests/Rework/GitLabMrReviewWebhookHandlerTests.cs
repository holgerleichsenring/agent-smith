using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Infrastructure.Services.Webhooks;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Webhooks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-f147: a GitLab Request changes, through the review handler and the admission;
/// 2026-10-08-0781: it nudges the ticket.</summary>
public sealed class GitLabMrReviewWebhookHandlerTests
{
    private static readonly DateTimeOffset At = new(2026, 10, 8, 12, 0, 0, TimeSpan.Zero);
    private const string MrUrl = "https://gitlab.example/o/app/-/merge_requests/4";
    private readonly Mock<IReworkNudges> _nudges = new();

    private GitLabMrReviewWebhookHandler Handler()
    {
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["app"] = new() { Name = "app", Repos = [new RepoConnection { Name = "app", Url = "https://gitlab.example/o/app", Type = RepoType.GitLab }] },
            },
        };
        var attempts = new Mock<IPreviousAttemptReader>();
        attempts.Setup(a => a.LatestAsync("app", "12", null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PreviousAttempt("run-1", "success", At.AddHours(-1), true));
        return new GitLabMrReviewWebhookHandler(PrReworkAdmissionTests.Admission(attempts.Object, _nudges.Object, config),
            NullLogger<GitLabMrReviewWebhookHandler>.Instance);
    }

    private void VerifyNudged(Times times) =>
        _nudges.Verify(n => n.EnqueueAsync(It.Is<ReworkNudgeRequest>(r => r.Project == "app" && r.TicketId == "12"
            && r.PrUrl == MrUrl && r.Origin == ReworkNudgeOrigin.PullRequest), It.IsAny<CancellationToken>()), times);

    private static string Update(string changes = "{}", string extra = "", string state = "requested_changes") => $$"""
        { "object_kind": "merge_request",
          "project": { "id": 5, "web_url": "https://gitlab.example/o/app" },
          "object_attributes": { "action": "update", "iid": 4, "url": "{{MrUrl}}", "source_branch": "agent-smith/12",
            "source_project_id": 5, "target_project_id": 5, "author_id": 2 {{extra}} },
          "reviewers": [ { "id": 9, "username": "alice", "state": "{{state}}" } ],
          "changes": {{changes}} }
        """;

    private static readonly Dictionary<string, string> NoHeaders = new(StringComparer.OrdinalIgnoreCase);

    [Fact]
    public async Task GitLabMrReview_ReviewerInRequestedChanges_NudgesTheTicket()
    {
        var changes = """{ "reviewers": [ [ { "id": 9, "state": "review_started" } ], [ { "id": 9, "state": "requested_changes" } ] ] }""";

        (await Handler().HandleAsync(Update(changes), NoHeaders)).Handled.Should().BeTrue();

        VerifyNudged(Times.Once());
    }

    [Fact]
    public async Task GitLabMrReview_RepeatWithoutReviewerChanges_Enters()
    {
        (await Handler().HandleAsync(Update(), NoHeaders)).Handled.Should().BeTrue();

        VerifyNudged(Times.Once());
    }

    [Fact]
    public async Task GitLabMrReview_TitleEdit_NotHandled() =>
        (await Handler().HandleAsync(Update("""{ "title": { "previous": "a", "current": "b" } }"""), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task GitLabMrReview_NoRequestedChanges_NotHandled() =>
        (await Handler().HandleAsync(Update(state: "approved"), NoHeaders)).Handled.Should().BeFalse();

    [Fact]
    public async Task GitLabMrPush_WithStandingRequestedChanges_StillReachesPrReview()
    {
        var push = Update(extra: """, "oldrev": "abc" """);

        (await Handler().HandleAsync(push, NoHeaders)).Handled.Should().BeFalse();
        var order = new ServiceCollection().AddWebhookHandlers()
            .Where(d => d.ServiceType == typeof(IWebhookHandler)).Select(d => d.ImplementationType).ToList();
        order.Should().ContainInOrder(typeof(GitLabMrEventWebhookHandler), typeof(GitLabMrReviewWebhookHandler));
    }
}

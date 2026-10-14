using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;
using FluentAssertions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>The worker reads a trusted comment command beside the standing vote; the newer one is the act.</summary>
public sealed class ReworkPendingActsTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 12, 9, 0, 0, TimeSpan.Zero);
    private static readonly RepoConnection Repo = new() { Name = "api", Type = RepoType.GitHub };
    private readonly Mock<ISourceProvider> _provider = new();
    private readonly InMemorySpecApprovalStore _approvals = new();

    private static PrReviewNote Note(string who, int minutes, string body = "x") =>
        new(new PrCommentAuthor("u", "r", who, who), false, false, Start.AddMinutes(minutes), body);

    private Task<PendingReworkAct?> NewestAsync()
    {
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(_provider.Object);
        var trust = new Mock<IPrReviewAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.Is<PrCommentAuthor>(a => a.AuthorId != "mallory"), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var project = new ResolvedProject { Name = "p", Repos = [Repo] };
        var attempt = new PreviousAttempt("run-1", "success", Start, true)
        {
            PullRequestUrls = new Dictionary<string, string> { ["api"] = "https://github.com/o/api/pull/4" },
        };
        return new ReworkPendingActs(Mock.Of<ITicketProviderFactory>(), sources.Object, trust.Object, _approvals)
            .NewestAsync(project, "42", attempt, CancellationToken.None);
    }

    private void Review(IReadOnlyList<PrReviewNote> votes, params PrReviewNote[] notes)
    {
        _provider.As<IPrReviewActReader>().Setup(r => r.ChangesRequestedAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(votes);
        _provider.As<IPrReviewThreadReader>().Setup(r => r.ListAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([.. notes.Select(n => new PrReviewThread(null, null, null, [n]))]);
    }

    [Fact]
    public async Task NewestAsync_CommandAfterVote_IsTheAct()
    {
        Review([Note("bob", 10)], Note("alice", 5, "rename it"), Note("alice", 20, "/agentsmith\nand the tests"));

        var act = await NewestAsync();

        act!.Act.Should().Be(new ReworkAct("alice", Start.AddMinutes(20), ReworkChannel.PullRequest)
        {
            Source = new AttemptSource(AttemptSourceKinds.Command, "https://github.com/o/api/pull/4"),
        });
        act.Repo.Should().Be(Repo);
    }

    [Fact]
    public async Task NewestAsync_UntrustedCommand_IsNoAct()
    {
        Review([], Note("mallory", 20, "/agent-smith"));

        (await NewestAsync()).Should().BeNull();
    }

    /// <summary>2026-10-13-9a59m: the stored approval beside the ticket's keyword comment — the newer is the act.</summary>
    [Fact]
    public async Task ReworkPendingActs_OperatorAndKeywordAct_NewerWins()
    {
        var tracker = new Mock<ITicketProvider>();
        tracker.Setup(t => t.GetCommentsAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentSmith.Domain.Entities.TicketComment("bob", Start.AddMinutes(10), "agentsmith again") { Id = "c-1" }]);
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(tracker.Object);
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            GithubTrigger = new WebhookTriggerConfig { CommentKeyword = "agentsmith" },
        };
        var attempt = new PreviousAttempt("run-1", "failed", Start, true);
        var acts = new ReworkPendingActs(tickets.Object, Mock.Of<ISourceProviderFactory>(), Mock.Of<IPrReviewAuthorTrust>(), _approvals);

        await _approvals.SaveAsync(Operator.OperatorActTests.Record(Start.AddMinutes(20)) with { Key = "github-7" }, default);
        var approval = await acts.NewestAsync(project, "7", attempt, CancellationToken.None);
        await _approvals.SaveAsync(Operator.OperatorActTests.Record(Start.AddMinutes(5)) with { Key = "github-7" }, default);
        var comment = await acts.NewestAsync(project, "7", attempt, CancellationToken.None);
        var answer = await acts.NewestHandbackAnswerAsync(project, "7", attempt, CancellationToken.None);

        approval!.Act.Channel.Should().Be(ReworkChannel.Operator, "the approval at minute 20 is newer than the comment at 10");
        approval.Act.Author.Should().Be("owner");
        approval.Repo.Should().BeNull("the worker answers an approval on the ticket");
        comment!.Act.Channel.Should().Be(ReworkChannel.Ticket, "the comment at 10 is newer than the approval at 5");
        answer!.Act.Channel.Should().Be(ReworkChannel.Operator, "a keyword comment never answers a hand-back");
    }

    /// <summary>2026-10-13-9a59n: the attempt's stored Retry beside the ticket's keyword comment — the newer is the act.</summary>
    [Fact]
    public async Task ReworkPendingActs_RetryAndKeywordAct_NewerWins()
    {
        var tracker = new Mock<ITicketProvider>();
        tracker.Setup(t => t.GetCommentsAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentSmith.Domain.Entities.TicketComment("bob", Start.AddMinutes(10), "agentsmith again") { Id = "c-1" }]);
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(tracker.Object);
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            GithubTrigger = new WebhookTriggerConfig { CommentKeyword = "agentsmith" },
        };
        var acts = new ReworkPendingActs(tickets.Object, Mock.Of<ISourceProviderFactory>(), Mock.Of<IPrReviewAuthorTrust>(), _approvals);
        PreviousAttempt Retried(int minutes) => new("run-1", "cancelled", Start, true)
        {
            RetryRequestedBy = "ops-1", RetryRequestedAt = Start.AddMinutes(minutes),
        };

        var retry = await acts.NewestAsync(project, "7", Retried(20), CancellationToken.None);
        var comment = await acts.NewestAsync(project, "7", Retried(5), CancellationToken.None);
        var answer = await acts.NewestHandbackAnswerAsync(project, "7", Retried(20), CancellationToken.None);

        retry!.Act.Should().Be(new ReworkAct("ops-1", Start.AddMinutes(20), ReworkChannel.Retry));
        retry.Repo.Should().BeNull("the worker answers a retry on the ticket");
        comment!.Act.Channel.Should().Be(ReworkChannel.Ticket, "the comment at 10 is newer than the retry at 5");
        answer.Should().BeNull("a retry never answers a hand-back");
    }

    /// <summary>2026-10-13-8762p: an overruled default beside the ticket's keyword comment — the newer is the act, carrying every note.</summary>
    [Fact]
    public async Task ReworkPendingActs_NewerOverrule_WinsOverOlderKeywordAct()
    {
        var tracker = new Mock<ITicketProvider>();
        tracker.Setup(t => t.GetCommentsAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentSmith.Domain.Entities.TicketComment("bob", Start.AddMinutes(10), "agentsmith again") { Id = "c-1" }]);
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(tracker.Object);
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            GithubTrigger = new WebhookTriggerConfig { CommentKeyword = "agentsmith" },
        };
        var acts = new ReworkPendingActs(tickets.Object, Mock.Of<ISourceProviderFactory>(), Mock.Of<IPrReviewAuthorTrust>(), _approvals);
        PreviousAttempt Overruled(int minutes) => new("run-1", "success", Start, true)
        {
            Overrules = [new(7, "run-1", "2026-10-13-1234a", "Which queue?", "polling", "overruled", "use the bus", "ana", Start.AddMinutes(minutes))],
        };

        var overrule = await acts.NewestAsync(project, "7", Overruled(20), CancellationToken.None);
        var comment = await acts.NewestAsync(project, "7", Overruled(5), CancellationToken.None);

        overrule!.Act.Should().Be(new ReworkAct("ana", Start.AddMinutes(20), ReworkChannel.Overrule)
        {
            Source = new AttemptSource(AttemptSourceKinds.Default, Id: "7"),
        });
        overrule.Text.Should().Contain("Which queue?").And.Contain("polling").And.Contain("use the bus");
        comment!.Act.Channel.Should().Be(ReworkChannel.Ticket, "the comment at 10 is newer than the overrule at 5");
    }

    // 2026-10-13-9a59t: the pull-request act carries what the note says; a keyword act carries its comment.
    [Fact]
    public async Task ReworkPendingActs_PullRequestAct_CarriesNoteBody()
    {
        Review([], Note("alice", 20, "/agentsmith\nand the tests"));

        (await NewestAsync())!.Text.Should().Be("/agentsmith\nand the tests");
    }

    [Fact]
    public async Task ReworkPendingActs_VoteWithoutText_CarriesNoText()
    {
        Review([Note("bob", 10, " ")]);

        (await NewestAsync())!.Text.Should().BeNull();
    }

    [Fact]
    public async Task ReworkPendingActs_KeywordAct_CarriesCommentBody()
    {
        var tracker = new Mock<ITicketProvider>();
        tracker.Setup(t => t.GetCommentsAsync(It.IsAny<AgentSmith.Domain.Models.TicketId>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([new AgentSmith.Domain.Entities.TicketComment("bob", Start.AddMinutes(10), "agentsmith add CSV") { Id = "c-1" }]);
        var tickets = new Mock<ITicketProviderFactory>();
        tickets.Setup(t => t.Create(It.IsAny<TrackerConnection>())).Returns(tracker.Object);
        var project = new ResolvedProject
        {
            Name = "p", Tracker = new TrackerConnection { Name = "gh", Type = TrackerType.GitHub },
            GithubTrigger = new WebhookTriggerConfig { CommentKeyword = "agentsmith" },
        };

        var act = await new ReworkPendingActs(tickets.Object, Mock.Of<ISourceProviderFactory>(), Mock.Of<IPrReviewAuthorTrust>(), _approvals)
            .NewestAsync(project, "42", new PreviousAttempt("run-1", "success", Start, true), CancellationToken.None);

        act!.Text.Should().Be("agentsmith add CSV");
    }
}

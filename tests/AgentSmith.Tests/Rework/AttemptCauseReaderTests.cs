using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Persistence;
using AgentSmith.Application.Services.Rework;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Reviews;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Webhooks;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;
using AgentSmith.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>
/// 2026-10-13-9a59d: the cause an attempt records is read from the act slots as FetchTicket left
/// them — a pull-request act first, a status move above a ticket comment, then requested, retrigger,
/// and the first pickup naming who moved the ticket.
/// </summary>
public sealed class AttemptCauseReaderTests
{
    private static readonly DateTimeOffset Start = new(2026, 10, 13, 9, 0, 0, TimeSpan.Zero);
    private const string PrUrl = "https://github.com/o/api/pull/4";

    private readonly Mock<ITicketProvider> _provider = new();
    private readonly PipelineContext _pipeline = new();
    private readonly InMemorySpecApprovalStore _approvals = new();

    private AttemptCauseReader Reader() => new(
        new TrackerIdentity(Mock.Of<ITicketProviderFactory>(), NullLogger<TrackerIdentity>.Instance),
        ApprovedSetDoubles.Resolver(_approvals), NullLogger<AttemptCauseReader>.Instance);

    private FetchTicketContext Context(IReadOnlyList<string>? statuses = null) =>
        new(new TicketId("42"), new TrackerConnection { Name = "gh", Type = TrackerType.GitHub }, _pipeline,
            "agentsmith", statuses);

    private Task<AttemptCause> ReadAsync(IReadOnlyList<string>? statuses = null) =>
        Reader().ReadAsync(_provider.Object, Context(statuses), CancellationToken.None);

    private void PreviousAttempt() =>
        _pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("run-1", "success", Start, true)
        {
            PullRequestUrls = new Dictionary<string, string> { ["api"] = PrUrl },
        });

    private void KeywordComment(int minutes, string id = "c-77")
    {
        _pipeline.Set(ContextKeys.TicketComments, (IReadOnlyList<TicketComment>)
            [new TicketComment("bob", Start.AddMinutes(minutes), "agentsmith please redo") { Id = id }]);
        ReworkActReader.Apply(_pipeline, "agentsmith");
    }

    private Mock<ITicketStatusHistory> History() => _provider.As<ITicketStatusHistory>();

    private void MovedBy(string login, int minutes, string status = "To Do") =>
        History().Setup(h => h.NewestPersonMoveIntoAsync(It.IsAny<TicketId>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<TrackerActor?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TicketStatusMove(new TrackerActor("id-" + login, login), Start.AddMinutes(minutes), status));

    private static PrReviewNote Note(int minutes, string body, string hostId) =>
        new(new PrCommentAuthor("u", "o/api", "alice", "alice"), false, false, Start.AddMinutes(minutes), body)
        {
            HostId = hostId,
        };

    private async Task ReadPullRequestAsync(PrReviewNote? vote, PrReviewNote? command)
    {
        var source = new Mock<ISourceProvider>();
        source.As<IPrReviewActReader>().Setup(a => a.ChangesRequestedAsync(PrUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(vote is null ? [] : [vote]);
        source.As<IPrReviewThreadReader>().Setup(r => r.ListAsync(PrUrl, It.IsAny<CancellationToken>()))
            .ReturnsAsync(command is null ? [] : [new PrReviewThread(null, null, null, [command])]);
        var sources = new Mock<ISourceProviderFactory>();
        sources.Setup(s => s.Create(It.IsAny<RepoConnection>())).Returns(source.Object);
        var trust = new Mock<IPrReviewAuthorTrust>();
        trust.Setup(t => t.IsTrustedAsync(It.IsAny<RepoType>(), It.IsAny<PrCommentAuthor>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _pipeline.Set(ContextKeys.Repos, (IReadOnlyList<RepoConnection>)[new RepoConnection { Name = "api", Type = RepoType.GitHub }]);
        await new PrReviewFeedbackFetcher(sources.Object, trust.Object, NullLogger<PrReviewFeedbackFetcher>.Instance)
            .FetchAsync(_pipeline, CancellationToken.None);
    }

    [Fact]
    public async Task AttemptCause_KeywordComment_RecordsTicketCommentWithItsId()
    {
        PreviousAttempt();
        KeywordComment(30);

        var cause = await ReadAsync();

        cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.TicketComment, "bob", Start.AddMinutes(30),
            new AttemptSource(AttemptSourceKinds.Comment, Id: "c-77")));
    }

    [Fact]
    public async Task AttemptCause_RequestChanges_RecordsPullRequestReviewWithUrlAndHostId()
    {
        PreviousAttempt();
        await ReadPullRequestAsync(Note(30, string.Empty, "PRR_kw1"), null);

        var cause = await ReadAsync();

        cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.PullRequest, "alice", Start.AddMinutes(30),
            new AttemptSource(AttemptSourceKinds.Review, PrUrl, "PRR_kw1")));
    }

    [Fact]
    public async Task AttemptCause_CommentCommand_RecordsPullRequestCommand()
    {
        PreviousAttempt();
        await ReadPullRequestAsync(Note(20, string.Empty, "PRR_old"), Note(30, "/agentsmith fix the tests", "IC_9"));

        var cause = await ReadAsync();

        cause.Channel.Should().Be(AttemptChannels.PullRequest);
        cause.Source.Should().Be(new AttemptSource(AttemptSourceKinds.Command, PrUrl, "IC_9"));
    }

    [Fact]
    public async Task AttemptCause_StatusMovedBack_RecordsStatusMoveWithActor()
    {
        PreviousAttempt();
        MovedBy("carol", 30, "Ready");
        await new StatusBackActReader(
                new TrackerIdentity(Mock.Of<ITicketProviderFactory>(), NullLogger<TrackerIdentity>.Instance),
                NullLogger<StatusBackActReader>.Instance)
            .ApplyAsync(_provider.Object, Context(["Ready"]), CancellationToken.None);

        var cause = await ReadAsync(["Ready"]);

        cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.StatusMove, "carol", Start.AddMinutes(30),
            new AttemptSource(AttemptSourceKinds.Status, Detail: "Ready")));
    }

    [Fact]
    public async Task AttemptCause_PrActAndNewerStatusMove_RecordsThePullRequest()
    {
        PreviousAttempt();
        await ReadPullRequestAsync(Note(30, string.Empty, "PRR_1"), null);
        _pipeline.Set(ContextKeys.StatusBackAct, new ReworkAct("carol", Start.AddMinutes(50), ReworkChannel.Status));

        (await ReadAsync()).Channel.Should().Be(AttemptChannels.PullRequest);
    }

    [Fact]
    public async Task AttemptCause_KeywordCommentNewerThanRequestChanges_RecordsTheComment()
    {
        PreviousAttempt();
        KeywordComment(40);
        await ReadPullRequestAsync(Note(30, string.Empty, "PRR_1"), null);

        var cause = await ReadAsync();

        cause.Channel.Should().Be(AttemptChannels.TicketComment);
        cause.Source!.Id.Should().Be("c-77");
    }

    [Fact]
    public async Task AttemptCause_RequestedByName_IsRequestedWithoutActor()
    {
        PreviousAttempt();
        _pipeline.Set(ContextKeys.RequestedByName, true);

        (await ReadAsync()).Should().Be(new AttemptCause(AttemptChannels.Requested));
    }

    // 2026-10-13-8762v: the series' own start is a named start too; its cause is asked first.
    [Fact]
    public async Task AttemptCauseReader_SeriesAutoStart_IsSeriesByAgentSmith()
    {
        PreviousAttempt();
        _pipeline.Set(ContextKeys.RequestedByName, true);
        _pipeline.Set(ContextKeys.SeriesAutoStart, System.Text.Json.JsonSerializer.SerializeToElement(true));

        (await ReadAsync()).Should().Be(new AttemptCause(AttemptChannels.Series, AttemptChannels.SeriesActor));
    }

    [Fact]
    public async Task AttemptCauseReader_ActBeatsSeriesAutoStart()
    {
        PreviousAttempt();
        _pipeline.Set(ContextKeys.RequestedByName, true);
        _pipeline.Set(ContextKeys.SeriesAutoStart, true);
        KeywordComment(30);

        (await ReadAsync()).Channel.Should().Be(AttemptChannels.TicketComment, "a person's act read by the attempt outranks the series");
    }

    // 2026-10-13-9a59n: a Retry carries no change — an act with content wins, a name or a re-trigger loses.
    private void RetriedAttempt(int minutes) =>
        _pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("run-1", "cancelled", Start, true)
        {
            RetryRequestedBy = "ops-1", RetryRequestedAt = Start.AddMinutes(minutes),
        });

    [Fact]
    public async Task AttemptCause_KeywordAfterRetry_IsKeyword()
    {
        RetriedAttempt(20);
        KeywordComment(30);

        (await ReadAsync()).Channel.Should().Be(AttemptChannels.TicketComment);
    }

    [Fact]
    public async Task AttemptCause_RetryOnly_IsRetryByThePrincipal_AboveRequestedByName()
    {
        RetriedAttempt(20);
        _pipeline.Set(ContextKeys.RequestedByName, true);

        (await ReadAsync()).Should().Be(new AttemptCause(AttemptChannels.Retry, "ops-1", Start.AddMinutes(20)));
    }

    // 2026-10-13-8762p: an overruled default ranks below the act slot and above a Retry.
    [Fact]
    public async Task AttemptCause_Overrule_IsChannelOverrule_WithTheDefaultAsSource()
    {
        _pipeline.Set(ContextKeys.PreviousAttempt, new PreviousAttempt("run-1", "success", Start, true)
        {
            RetryRequestedBy = "ops-1", RetryRequestedAt = Start.AddMinutes(30),
            Overrules = [new(7, "run-1", null, "Which queue?", "polling", "overruled", null, "ana", Start.AddMinutes(20))],
        });

        (await ReadAsync()).Should().Be(new AttemptCause(AttemptChannels.Overrule, "ana", Start.AddMinutes(20),
            new AttemptSource(AttemptSourceKinds.Default, Id: "7")));
        KeywordComment(10);
        (await ReadAsync()).Channel.Should().Be(AttemptChannels.TicketComment, "an act in the slot outranks an overrule");
    }

    [Fact]
    public async Task AttemptCause_NoActWithPreviousAttempt_IsRetrigger()
    {
        PreviousAttempt();
        MovedBy("carol", 30);

        (await ReadAsync(["Ready"])).Should().Be(new AttemptCause(AttemptChannels.Retrigger));
    }

    [Fact]
    public async Task AttemptCause_FirstAttempt_IsPickupWithTheMover()
    {
        MovedBy("dave", -60, "Ready");

        var cause = await ReadAsync(["Ready"]);

        cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.Pickup, "dave", Start.AddMinutes(-60),
            new AttemptSource(AttemptSourceKinds.Status, Detail: "Ready")));
    }

    [Fact]
    public async Task AttemptCause_HistoryUnreadable_IsPickupWithoutActor()
    {
        History().Setup(h => h.NewestPersonMoveIntoAsync(It.IsAny<TicketId>(), It.IsAny<IReadOnlyCollection<string>>(),
                It.IsAny<TrackerActor?>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new HttpRequestException("history unavailable"));

        (await ReadAsync(["Ready"])).Should().Be(new AttemptCause(AttemptChannels.Pickup));
    }

    /// <summary>2026-10-13-9a59m: FetchTicket runs before the run resolved its approval; the operator arm resolves
    /// the stored record itself, ranks first, and publishes the winner the later steps read.</summary>
    [Fact]
    public async Task AttemptCause_AtFetchTicket_ResolvesApprovalItself_CauseOperator()
    {
        PreviousAttempt();
        KeywordComment(40);
        _pipeline.Set(ContextKeys.Ticket, new Ticket(new TicketId("42"), "t", "d", null, "Failed", "github"));
        _pipeline.Set(ContextKeys.TrackerConnection, "gh");
        await _approvals.SaveAsync(Operator.OperatorActTests.Record(Start.AddMinutes(30)), default);

        var cause = await ReadAsync();

        cause.Should().BeEquivalentTo(new AttemptCause(AttemptChannels.Operator, "owner", Start.AddMinutes(30),
            new AttemptSource(AttemptSourceKinds.Conversation, Id: "s-1")), "the approval ranks above a later keyword comment");
        _pipeline.Has(ContextKeys.ApprovedSpecSet).Should().BeTrue("the resolver published the winner for ScopeRepos and DeriveSpec");
    }
}

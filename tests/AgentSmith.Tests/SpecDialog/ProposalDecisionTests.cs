using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-11-10-ccd9: a stored proposal awaits a decision until a person approves or rejects the exact card they
/// saw or a newer proposal replaces it. Nothing waits, nothing expires, and one approval wins — across two
/// replicas too, which here are two units of work over one database.
/// </summary>
public sealed class ProposalDecisionTests : IDisposable
{
    private const string Platform = "slack";
    private const string Thread = "th-ccd9";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly Mock<IOutcomeSink> _sink = new();

    public ProposalDecisionTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = Context();
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Decision_ApproveWithStoredIdADayLater_ClaimsAndFiles()
    {
        var (state, id) = await ProposedAsync();

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, default)).Should().Be(SpecDialogDecided.Decided);

        Filed().Should().Be(1, "the card the person saw is filed, however long it waited");
    }

    [Fact]
    public async Task Decision_TwoApprovesOnTwoReplicas_OneClaimWins()
    {
        var (state, id) = await ProposedAsync();
        await using var other = Context();

        var first = await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default);
        var second = await new SpecDialogProposalClaim(other).ClaimAsync(Platform, Thread, id, default);

        first.Should().NotBeNull();
        second.Should().BeNull("the first claim moved the proposal out of the awaiting slot");
    }

    [Fact]
    public async Task Decision_ContinuationActApprovedTwice_SecondClaimFails()
    {
        var (state, id) = await ProposedAsync();
        var decisions = Harness().Decisions();

        await decisions.DecideAsync(state, SpecDialogDecision.Approved, id, false, default);
        await decisions.DecideAsync(state, SpecDialogDecision.Approved, id, false, default);

        Filed().Should().Be(1, "the second approve finds the card claimed and acts on nothing");
    }

    [Fact]
    public async Task Decision_OlderId_AppendsReplacedNoticeAndRunsNoTurn()
    {
        var (state, older) = await ProposedAsync();
        await Harness().Flow().HandleAsync(state, Spec("p2"), default);

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, older, false, default)).Should().Be(SpecDialogDecided.Replaced);

        Filed().Should().Be(0);
        (await TranscriptAsync()).Should().Contain(t => t.Kind == SpecDialogTurnKind.Notice && t.Text.Contains("replaced"));
    }

    [Fact]
    public async Task Steward_Supersede_ThenClickOnOldId_GetsNotice()
    {
        var (state, id) = await ProposedAsync();
        await Harness().Flow().HandleAsync(state, new AnswerOutcome(), default, stewarded: true);

        await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, default);

        Filed().Should().Be(0);
        (await TranscriptAsync()).Should().Contain(t => t.Text.Contains("no longer open"));
    }

    [Fact]
    public async Task Claim_MovesTheProposalToConfirmedAndFreesTheSlot()
    {
        var (_, id) = await ProposedAsync();

        await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default);

        var row = await RowAsync();
        row.ConfirmedOutcomeJson.Should().NotBeNull();
        row.AwaitingProposalJson.Should().BeNull("the next turn may propose at once");
        row.ProposalState.Should().Be(SpecDialogProposalClaim.Filing);
    }

    // L1: every write lands on the thread's newest open row — the one every read answers from — never on all of them.
    [Fact]
    public async Task Store_TwoOpenRowsOnTheThread_WritesOnlyTheNewest()
    {
        var (state, id) = await ProposedAsync();
        _context.Add(new SpecDialogSession
        {
            SessionId = "s-newer", Platform = Platform, ChannelId = "C1", ThreadId = Thread, UserId = "U1", Project = "sample", IsOpen = true,
        });
        await _context.SaveChangesAsync();

        await Harness().Flow().HandleAsync(state, Spec("p2"), default);
        (await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default)).Should().BeNull();

        var rows = await Context().Set<SpecDialogSession>().Where(s => s.ThreadId == Thread).OrderBy(s => s.Id).ToListAsync();
        (rows[0].ProposalId, rows[0].ProposalState).Should().Be((id, SpecDialogProposalClaim.Awaiting), "the older row is left as it was");
        rows[1].ProposalId.Should().NotBeNull().And.NotBe(id);
    }

    [Fact]
    public async Task Claim_AwaitingJsonNull_Fails()
    {
        var (_, id) = await ProposedAsync();
        await _context.Set<SpecDialogSession>().ExecuteUpdateAsync(s => s.SetProperty(r => r.AwaitingProposalJson, (string?)null));

        (await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default)).Should().BeNull();
    }

    [Fact]
    public async Task Sink_AfterFiling_DoesNotClearANewerProposal()
    {
        var (state, id) = await ProposedAsync();
        // The next turn proposes while the approved one is being filed.
        _sink.Setup(s => s.AcceptAsync(It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .Returns(async () => { await Harness().Flow().HandleAsync(state, Spec("p2"), default); return OutcomeAcceptance.Acted; });

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, default)).Should().Be(SpecDialogDecided.Decided);

        Filed().Should().Be(1, "the decision path called the sink");
        var held = await Harness().Outcomes.ReadAsync(Platform, Thread, default);
        ((SpecOutcome)held.Proposal!).Draft.SpecId.Should().Be("p2", "a finished filing never touches the awaiting slot, which the claim freed");
    }

    // H1: an approval the sink refuses after the claim filed nothing, so the proposal is decidable again.
    [Fact]
    public async Task Approve_SinkRefuses_ProposalAwaitsAgainUnderItsId()
    {
        var (state, id) = await ProposedAsync();
        _sink.Setup(s => s.AcceptAsync(It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<bool>(), It.IsAny<CancellationToken>(), It.IsAny<string?>()))
            .ReturnsAsync(OutcomeAcceptance.Refused);

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, default)).Should().Be(SpecDialogDecided.Refused);

        var row = await RowAsync();
        (row.ProposalId, row.ProposalState, row.ConfirmedOutcomeJson).Should().Be((id, SpecDialogProposalClaim.Awaiting, (string?)null));
        row.AwaitingProposalJson.Should().NotBeNull("the refused approval kept the card the person saw");
    }

    [Fact]
    public async Task Release_NewerProposalStoredWhileFiling_KeepsTheNewer()
    {
        var (state, id) = await ProposedAsync();
        await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default);
        await Harness().Flow().HandleAsync(state, Spec("p2"), default);

        (await Harness().Proposals.ReleaseAsync(state, id, default)).Should().BeFalse("a newer proposal took the slot");

        var held = await Harness().Outcomes.ReadAsync(Platform, Thread, default);
        ((SpecOutcome)held.Proposal!).Draft.SpecId.Should().Be("p2");
    }

    // H1: refused by the conversation's shape alone, so nothing is claimed in the first place.
    [Fact]
    public async Task Approve_CommentInAnUnboundConversation_RefusedBeforeTheClaim()
    {
        var state = await Harness().OpenAsync(Platform, Thread);
        await Harness().Flow().HandleAsync(state, new TicketCommentOutcome("a comment"), default);
        var id = (await Harness().Outcomes.ReadAsync(Platform, Thread, default)).ProposalId!;

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, default)).Should().Be(SpecDialogDecided.Refused);

        Filed().Should().Be(0);
        (await RowAsync()).ProposalState.Should().Be(SpecDialogProposalClaim.Awaiting);
        (await TranscriptAsync()).Should().Contain(t => t.Kind == SpecDialogTurnKind.Notice && t.Text == SpecDialogApprovalRefusal.UnboundComment);
    }

    [Fact]
    public async Task Observer_MidReviewDraft_WritesOnlyTheReviewingColumn()
    {
        var (state, id) = await ProposedAsync();

        await Harness().Proposals.StoreReviewingAsync(state, Spec("p2") with { UnderReview = true }, default);

        var row = await RowAsync();
        row.ProposalId.Should().Be(id, "the awaiting proposal is untouched");
        row.ReviewingProposalJson.Should().NotBeNull();
    }

    [Fact]
    public async Task Reject_SingleCompareAndSet_ClearsTheCard()
    {
        var (state, id) = await ProposedAsync();

        await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Rejected, id, false, default);

        (await Harness().Outcomes.ReadAsync(Platform, Thread, default)).Proposal.Should().BeNull();
        (await new SpecDialogProposalClaim(_context).RejectAsync(Platform, Thread, id, default)).Should().BeFalse(
            "a second reject finds nothing awaiting");
    }

    [Fact]
    public async Task SlackTypedApprove_BindsToPostedProposalIdOnlyAfterPostedAt()
    {
        var (state, id) = await ProposedAsync();
        await Harness().Proposals.PostedAsync(state, id, DateTimeOffset.UtcNow.AddMinutes(-1), default);
        var current = (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!;

        (await Harness().Decisions().DecideAsync(current, SpecDialogDecision.Approved, null, false, default)).Should().Be(SpecDialogDecided.Decided);

        Filed().Should().Be(1, "a typed word in the thread names the card that thread was last shown");
    }

    [Fact]
    public async Task TypedWord_CardNotLatestEntry_IsAnOrdinaryMessage()
    {
        var (state, id) = await ProposedAsync();
        await Harness().Proposals.PostedAsync(state, id, DateTimeOffset.UtcNow.AddMinutes(-1), default);
        await Harness().Sessions.AppendTurnAsync(Platform, Thread, TranscriptRole.Assistant, "Because the store is shared.",
            SpecDialogTurnKind.Answer, null, default);
        var current = (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!;

        (await Harness().Decisions().DecideAsync(current, SpecDialogDecision.Approved, null, false, default)).Should().Be(
            SpecDialogDecided.No, "after a later agent reply a typed 'yes' answers that reply, not the card");
        Filed().Should().Be(0);
    }

    [Fact]
    public async Task SlackPostFails_PostedProposalIdUnchanged()
    {
        var (_, _) = await ProposedAsync();

        (await RowAsync()).PostedProposalId.Should().BeNull("no thread adapter took the card, so nothing was posted");
    }

    [Fact]
    public async Task LazyBackfill_RowWithoutId_MintsOneByCompareAndSet()
    {
        await Harness().OpenAsync(Platform, Thread);
        await _context.Set<SpecDialogSession>().ExecuteUpdateAsync(s =>
            s.SetProperty(r => r.LatestProposalJson, OutcomeProposalJson.Write(Spec("p1"))));

        await Harness().Proposals.BackfillAsync(Platform, Thread, default);

        var held = await Harness().Outcomes.ReadAsync(Platform, Thread, default);
        held.ProposalId.Should().NotBeNullOrEmpty();
        held.Proposal.Should().NotBeNull();
    }

    // M3: a typed word binds by the platform's own time — one Slack took before the card was posted names nothing.
    [Fact]
    public async Task SlackTypedApprove_SentBeforePostedAt_IsAnOrdinaryMessage()
    {
        var (state, id) = await ProposedAsync();
        var postedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
        await Harness().Proposals.PostedAsync(state, id, postedAt, default);
        var current = (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!;
        var decisions = Harness().Decisions();

        (await decisions.DecideAsync(current, SpecDialogDecision.Approved, null, false, default, postedAt.AddSeconds(-2)))
            .Should().Be(SpecDialogDecided.No, "the word was written before the card reached the thread");
        (await decisions.DecideAsync(current, SpecDialogDecision.Approved, null, false, default, postedAt.AddSeconds(2)))
            .Should().Be(SpecDialogDecided.Decided);
        Filed().Should().Be(1);
    }

    // L6: a proposal that asks the person something is decided by its buttons; a bare typed word is a message.
    [Fact]
    public async Task TypedWord_ProposalWithOpenQuestions_IsAnOrdinaryMessage()
    {
        var state = await Harness().OpenAsync(Platform, Thread);
        await Harness().Flow().HandleAsync(state, Spec("p1") with { Questions = [new OpenQuestion("Which store?", "Redis", ["p1"])] }, default);
        var id = (await Harness().Outcomes.ReadAsync(Platform, Thread, default)).ProposalId!;
        await Harness().Proposals.PostedAsync(state, id, DateTimeOffset.UtcNow.AddMinutes(-1), default);
        var current = (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!;

        (await Harness().Decisions().DecideAsync(current, SpecDialogDecision.Approved, null, false, default))
            .Should().Be(SpecDialogDecided.No);
        (await Harness().Decisions().DecideAsync(current, SpecDialogDecision.Approved, id, false, default))
            .Should().Be(SpecDialogDecided.Decided, "the button names the card and decides it");
    }

    [Fact]
    public async Task Reject_SingleCompareAndSet_FailsTheCallerOnError()
    {
        var (state, id) = await ProposedAsync();
        await _context.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER no_write BEFORE UPDATE ON SpecDialogSessions BEGIN SELECT RAISE(ABORT, 'store unavailable'); END;");

        var reject = () => Harness().Decisions().DecideAsync(state, SpecDialogDecision.Rejected, id, false, default);

        await reject.Should().ThrowAsync<SqliteException>("a reject that did not land must not be reported as done");
        await _context.Database.ExecuteSqlRawAsync("DROP TRIGGER no_write;");
        (await TranscriptAsync()).Should().NotContain(t => t.Kind == SpecDialogTurnKind.Notice);
        (await RowAsync()).ProposalId.Should().Be(id, "the card still awaits");
    }

    [Fact]
    public async Task OutcomeFlow_FinalProposal_IsStoredAwaitingBeforeThePush()
    {
        var hub = new RecordingDialogHub();
        string? storedAtPush = null;
        hub.OnPush = (method, _) =>
        {
            if (method == "SpecDialogProposal") storedAtPush ??= Context().Set<SpecDialogSession>().Single(s => s.IsOpen).AwaitingProposalJson;
        };
        var channel = new DashboardOutcomeChannel(new SpecDialogProposalComposer(new AgentSmith.Application.Services.SpecDialog.EpicChildOrderer(),
            new AgentSmith.Application.Services.SpecDialog.BugTicketRenderer()), Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardOutcomeChannel>.Instance, hub);
        var harness = new SpecDialogDecisionHarness(_context, channel, _sink.Object);
        var state = await harness.OpenAsync(DispatcherDefaults.PlatformDashboard, "d-push");

        await harness.Flow().HandleAsync(state, Spec("p1"), default);

        storedAtPush.Should().NotBeNull("the page is never pushed a card the row does not hold");
    }

    [Fact]
    public async Task SlackMismatchNotice_RepostsTheAwaitingCard()
    {
        var posted = new List<string>();
        var slack = new Mock<IChatThreadAdapter>();
        slack.SetupGet(a => a.Platform).Returns(Platform);
        slack.Setup(a => a.PostQuestionAsync(It.IsAny<ChatThread>(), It.IsAny<AgentSmith.Contracts.Dialogue.DialogQuestion>(), It.IsAny<CancellationToken>()))
            .Callback((ChatThread _, AgentSmith.Contracts.Dialogue.DialogQuestion q, CancellationToken _) => posted.Add(q.QuestionId))
            .ReturnsAsync(new PostedMessage("ts-1", DateTimeOffset.UtcNow)); // 205fh: Slack's own time
        var harness = new SpecDialogDecisionHarness(_context, sink: _sink.Object, threads: [slack.Object]);
        var state = await harness.OpenAsync(Platform, Thread);
        await harness.Flow().HandleAsync(state, Spec("p1"), default);
        var older = (await harness.Outcomes.ReadAsync(Platform, Thread, default)).ProposalId!;
        await harness.Flow().HandleAsync(state, Spec("p2"), default);
        var current = (await harness.Outcomes.ReadAsync(Platform, Thread, default)).ProposalId!;

        (await harness.Decisions().DecideAsync(state, SpecDialogDecision.Approved, older, false, default)).Should().Be(SpecDialogDecided.Replaced);

        posted.Should().Equal(older, current, current);
        (await RowAsync()).PostedProposalId.Should().Be(current, "a typed word in the thread now names the card it was shown again");
        Filed().Should().Be(0);
    }

    // L2: a row from before the deploy is given its id by the decision path too, not only by the page's read.
    [Fact]
    public async Task Decision_RowWithoutId_IsBackfilledFirst()
    {
        var state = await Harness().OpenAsync(Platform, Thread);
        await _context.Set<SpecDialogSession>().ExecuteUpdateAsync(s =>
            s.SetProperty(r => r.LatestProposalJson, OutcomeProposalJson.Write(Spec("p1"))));

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, "an-older-card", false, default))
            .Should().Be(SpecDialogDecided.Replaced);

        (await RowAsync()).ProposalId.Should().NotBeNull();
        (await TranscriptAsync()).Should().Contain(t => t.Text.Contains("the current proposal is"), "the replaced notice names the backfilled card as current");
    }

    private SpecDialogDecisionHarness Harness() => new(_context, sink: _sink.Object);

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private async Task<(ConversationState State, string Id)> ProposedAsync()
    {
        var state = await Harness().OpenAsync(Platform, Thread);
        await Harness().Flow().HandleAsync(state, Spec("p1"), default);
        return (state, (await Harness().Outcomes.ReadAsync(Platform, Thread, default)).ProposalId!);
    }

    private int Filed() => _sink.Invocations.Count(i => i.Method.Name == nameof(IOutcomeSink.AcceptAsync));

    private async Task<SpecDialogSession> RowAsync() =>
        await Context().Set<SpecDialogSession>().SingleAsync(s => s.ThreadId == Thread && s.IsOpen);

    private async Task<IReadOnlyList<TranscriptTurn>> TranscriptAsync() =>
        (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!.Transcript;

    private static SpecOutcome Spec(string id) => new(new SpecDraft(id, $"goal of {id}", $"spec: {id}", []));
}

using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
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

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, id, false, default)).Should().BeTrue();

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

        (await Harness().Decisions().DecideAsync(state, SpecDialogDecision.Approved, older, false, default)).Should().BeTrue();

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
        await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default);
        await Harness().Flow().HandleAsync(state, Spec("p2"), default);

        (await Harness().Outcomes.ReadAsync(Platform, Thread, default)).Proposal.Should().NotBeNull(
            "a finished filing never touches the awaiting slot, which the claim already freed");
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

        (await Harness().Decisions().DecideAsync(current, SpecDialogDecision.Approved, null, false, default)).Should().BeTrue();

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

        (await Harness().Decisions().DecideAsync(current, SpecDialogDecision.Approved, null, false, default)).Should().BeFalse(
            "after a later agent reply a typed 'yes' answers that reply, not the card");
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

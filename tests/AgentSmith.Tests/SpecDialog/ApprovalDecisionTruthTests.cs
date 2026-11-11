using AgentSmith.Contracts.Dialogue;
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
/// 2026-11-11-205fe: a stored decision says what happened — a refused or replaced click keeps no approval, a decision
/// whose proposal still awaits reads as not applied — and the confirmed slot belongs to the proposal that claimed it,
/// so another filing never clears it and a release never empties the awaiting slot. Two units of work over one SQLite
/// database stand for two replicas.
/// </summary>
public sealed class ApprovalDecisionTruthTests : IDisposable
{
    private const string Platform = "slack";
    private const string Thread = "th-205fe";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly Mock<IOutcomeSink> _sink = new();

    public ApprovalDecisionTruthTests()
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
    public async Task Decision_ClickOnReplacedCard_StoredTurnHasNoDecision()
    {
        await ProposedAsync();

        var admitted = await Admission().AdmitAsync("approve", "U1", Platform, Thread, default, proposalId: "replaced-card");
        var decided = await Outcome().DecideAsync(admitted!.State, SpecDialogDecision.Approved, "replaced-card", false, default);

        decided.Should().Be(SpecDialogDecided.Replaced);
        (await LastUserTurnAsync()).Decision.Should().BeNull("a click on a replaced card approved nothing");
    }

    [Fact]
    public async Task Decision_SinkRefusesAfterClaim_StoredTurnHasNoDecision()
    {
        var (_, id) = await ProposedAsync();
        _sink.Setup(s => s.AcceptAsync(It.IsAny<ConversationState>(), It.IsAny<OutcomeProposal>(), It.IsAny<bool>(),
            It.IsAny<CancellationToken>(), It.IsAny<string?>())).ReturnsAsync(OutcomeAcceptance.Refused);

        var admitted = await Admission().AdmitAsync("approve", "U1", Platform, Thread, default, proposalId: id);
        var decided = await Outcome().DecideAsync(admitted!.State, SpecDialogDecision.Approved, id, false, default);

        decided.Should().Be(SpecDialogDecided.Refused);
        (await LastUserTurnAsync()).Decision.Should().BeNull("a refused approval filed nothing");
        (await RowAsync()).ProposalState.Should().Be(SpecDialogProposalClaim.Awaiting);
    }

    [Fact]
    public async Task View_DecidedTurnWhoseProposalStillAwaits_ReadsAsNotApplied()
    {
        var (_, id) = await ProposedAsync();
        // The server dies between storing the decided message and deciding it.
        await Admission().AdmitAsync("approve", "U1", Platform, Thread, default, proposalId: id);
        var transcript = (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!.Transcript;

        transcript[^1].DecisionProposalId.Should().Be(id);
        SpecDialogShownTranscript.Turns(transcript, id)[^1].Decision.Should().BeNull("its proposal still awaits");
        SpecDialogShownTranscript.Turns(transcript, null)[^1].Decision.Should().Be("approved", "once claimed it was applied");
    }

    [Fact]
    public async Task Claim_FirstFilingCompletesAfterSecondClaim_SecondFilingsConfirmedJsonUntouched()
    {
        var (_, first) = await ProposedAsync();
        await using var other = Context();
        (await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, first, default)).Should().NotBeNull();
        var (second, _) = await new SpecDialogProposalClaim(other).SetAwaitingAsync(Platform, Thread, Json("p2"), default);
        (await new SpecDialogProposalClaim(other).ClaimAsync(Platform, Thread, second, default)).Should().NotBeNull();

        (await Slot(_context).ClearAsync(Platform, Thread, first, default)).Should().BeFalse("the slot is the second's now");

        var row = await RowAsync();
        (row.ConfirmedProposalId, row.ConfirmedOutcomeJson).Should().Be((second, Json("p2")));
        (await Slot(other).ReleaseAsync(Platform, Thread, second, default)).Should().BeTrue();
        row = await RowAsync();
        (row.ProposalId, row.ProposalState, row.AwaitingProposalJson).Should().Be((second, SpecDialogProposalClaim.Awaiting, Json("p2")));
    }

    [Fact]
    public async Task Release_ConfirmedJsonCleared_ChangesNothing()
    {
        var (_, id) = await ProposedAsync();
        await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default);
        await Context().Set<SpecDialogSession>().ExecuteUpdateAsync(s => s.SetProperty(r => r.ConfirmedOutcomeJson, (string?)null));

        (await Slot(_context).ReleaseAsync(Platform, Thread, id, default)).Should().BeFalse();

        var row = await RowAsync();
        (row.ProposalState, row.AwaitingProposalJson).Should().Be((SpecDialogProposalClaim.Filing, (string?)null),
            "a release never leaves the awaiting slot without its proposal");
    }

    [Fact]
    public async Task ClearConfirmed_OtherProposalsId_ChangesNothing()
    {
        var (_, id) = await ProposedAsync();
        await new SpecDialogProposalClaim(_context).ClaimAsync(Platform, Thread, id, default);

        (await Slot(_context).ClearAsync(Platform, Thread, "another", default)).Should().BeFalse();
        (await Slot(_context).ClearAsync(Platform, Thread, null, default)).Should().BeFalse();

        (await RowAsync()).ConfirmedOutcomeJson.Should().NotBeNull();
        (await Slot(_context).ClearAsync(Platform, Thread, id, default)).Should().BeTrue();
        (await RowAsync()).ConfirmedOutcomeJson.Should().BeNull();
    }

    private SpecDialogDecisionHarness Harness() => new(_context, sink: _sink.Object);

    private SpecDialogAnswerAdmission Admission() => new(Harness().Sessions,
        new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)), Mock.Of<IDialogueTransport>(), Harness().Proposals);

    private SpecDialogDecisionOutcome Outcome() => new(Harness().Decisions(), Admission());

    private static SpecDialogConfirmedSlot Slot(AgentSmithDbContext context) => new(new SpecDialogProposalClaim(context));

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private async Task<(ConversationState State, string Id)> ProposedAsync()
    {
        var state = await Harness().OpenAsync(Platform, Thread);
        await Harness().Flow().HandleAsync(state, Spec("p1"), default);
        return (state, (await Harness().Outcomes.ReadAsync(Platform, Thread, default)).ProposalId!);
    }

    private async Task<TranscriptTurn> LastUserTurnAsync() =>
        (await Harness().Sessions.GetOpenByThreadAsync(Platform, Thread, default))!.Transcript.Last(t => t.Role == TranscriptRole.User);

    private async Task<SpecDialogSession> RowAsync() =>
        await Context().Set<SpecDialogSession>().SingleAsync(s => s.ThreadId == Thread && s.IsOpen);

    private static string Json(string id) => OutcomeProposalJson.Write(Spec(id));

    private static SpecOutcome Spec(string id) => new(new SpecDraft(id, $"goal of {id}", $"spec: {id}", []));
}

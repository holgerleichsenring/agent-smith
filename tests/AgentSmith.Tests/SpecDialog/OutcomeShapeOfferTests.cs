using AgentSmith.Tests.TestSupport;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-355b: the confirmation offers the other SHAPES the proposal could take beside
/// approving and rejecting it. The shapes are derived in code from the typed proposal's own
/// kind — nothing is asked of the model and nothing is stored — and a picked one is fed into
/// the edit door that already exists, so the master re-proposes and the operator approves THAT.
/// <para>
/// What has to hold for that to be safe is in here: the question STAYS an approval, the labels
/// are colon-free and short enough for the surface that answers by action id, and no label is
/// an approval or rejection word to the reader the confirmer actually uses.
/// </para>
/// </summary>
public sealed class OutcomeShapeOfferTests : IDisposable
{
    private const string Platform = "slack";
    private const string Thread = "th-355b";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public OutcomeShapeOfferTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Confirmation_AOneSpecProposal_OffersCuttingItIntoSeveral()
    {
        var asked = await AskedAsync(new SpecOutcome(Draft("p9001")));

        asked.Type.Should().Be(QuestionType.Approval,
            "the pair, the recorded decision and the pane's counted summary are keyed off the kind");
        asked.Choices!.Select(choice => choice.Label).Should().Contain("Cut into several specs");
    }

    [Fact]
    public async Task Confirmation_AMultiSpecProposal_OffersCuttingItIntoOne()
    {
        var asked = await AskedAsync(new EpicOutcome(Draft("p9000"), [Draft("p9000a")]));

        asked.Type.Should().Be(QuestionType.Approval);
        asked.Choices!.Select(choice => choice.Label).Should().Equal("Make it one spec");
    }

    [Fact]
    public async Task Confirmation_ABugProposal_OffersMakingItASpec()
    {
        var asked = await AskedAsync(
            new BugOutcome(new BugTicketDraft("Widget drops", "It drops.", "It stops.")));

        asked.Type.Should().Be(QuestionType.Approval);
        asked.Choices!.Select(choice => choice.Label).Should().Equal("Make it a spec");
    }

    /// <summary>
    /// The arithmetic the surfaces bind by, pinned here rather than read off a published limit:
    /// a question id is a thirty-two character identifier, so a colon plus a label of at most
    /// forty characters is at most seventy-three characters of action id, and approve, reject
    /// and every shape share ONE action block.
    /// </summary>
    [Fact]
    public void Confirmation_EveryOfferedLabel_IsColonFreeAndShortEnoughForEverySurface()
    {
        const int labelCeiling = 40;
        const int actionElementCeiling = 25;

        OutcomeShapes.All.Should().NotBeEmpty();
        OutcomeShapes.All.Should().OnlyContain(shape => !shape.Label.Contains(':'),
            "one surface answers a button with the tail of its action id after the LAST colon, "
            + "so a colon in a label drops the click in silence");
        OutcomeShapes.All.Should().OnlyContain(shape => shape.Label.Length <= labelCeiling);
        OutcomeShapes.MaxLabelLength.Should().Be(labelCeiling);
        foreach (var proposal in EveryConfirmedKind())
            (2 + OutcomeShapes.For(proposal).Count).Should().BeLessThanOrEqualTo(actionElementCeiling);
    }

    [Fact]
    public void Confirmation_EveryOfferedLabel_IsNeitherAnApprovalNorARejectionWord() =>
        OutcomeShapes.All.Should().OnlyContain(
            shape => SpecDialogAnswerWords.DecisionIn(shape.Label) == null,
            "an approval or rejection word is matched FIRST, so a label that was one could "
            + "never reach the edit door it is supposed to open");

    /// <summary>
    /// Through the reader the confirmer uses, not against a raw list: it trims and lowercases,
    /// so a label that differed from an approval word only by case or padding would still be
    /// read as one.
    /// </summary>
    [Fact]
    public void Confirmation_EveryOfferedLabel_IsNoDecisionToTheReaderTheConfirmerUses()
    {
        foreach (var shape in OutcomeShapes.All)
        foreach (var asSent in new[]
                 {
                     shape.Label, $"  {shape.Label}  ",
                     shape.Label.ToUpperInvariant(), shape.Label.ToLowerInvariant(),
                 })
            SpecDialogAnswerWords.DecisionIn(asSent).Should().BeNull(
                $"'{asSent}' must reach the master as a direction, not as a decision");
    }

    [Fact]
    public async Task Confirmation_AReplyMatchingAnOfferedLabel_IsAnEditCarryingThatLabel()
    {
        var proposal = new SpecOutcome(Draft("p9001"));
        var label = OutcomeShapes.For(proposal)[0].Label;

        var result = await ConfirmAsync(proposal, label);

        result.Should().BeOfType<OutcomeEditRequested>()
            .Which.Note.Should().Be(label,
                "the picked shape is the note the master is asked to re-propose in");
    }

    [Fact]
    public async Task Confirmation_AReplyMatchingAnApprovalWord_StillApproves() =>
        (await ConfirmAsync(new SpecOutcome(Draft("p9001")), "approve"))
            .Should().BeOfType<OutcomeConfirmed>();

    [Fact]
    public async Task Confirmation_AnyOtherText_IsStillAnEditNote() =>
        (await ConfirmAsync(new SpecOutcome(Draft("p9001")), "drop the second slice"))
            .Should().BeOfType<OutcomeEditRequested>()
            .Which.Note.Should().Be("drop the second slice");

    [Fact]
    public async Task OutcomeFlow_ATimedOutConfirmation_ClearsTheProposalAsItDoesToday()
    {
        var (flow, store, state) = await FlowAsync();
        var proposal = new SpecOutcome(Draft("p9001"));
        await store.SetProposalAsync(Platform, Thread, proposal, CancellationToken.None);

        var result = await flow.ApplyAsync(
            state, proposal, new OutcomeConfirmationTimedOut(), false, CancellationToken.None);

        result.Should().BeOfType<OutcomeFlowCompleted>();
        (await store.ReadAsync(Platform, Thread, CancellationToken.None)).Proposal.Should().BeNull(
            "a timed-out approval is over as surely as a rejected one — the pane must not "
            + "offer it after a reload");
    }

    /// <summary>
    /// The fall-through used to MEAN timeout: a fifth result would have cleared the stored
    /// proposal and told the thread nothing would be filed, destroying it in silence instead
    /// of failing to compile.
    /// </summary>
    [Fact]
    public async Task OutcomeFlow_AnUnknownConfirmation_Throws()
    {
        var (flow, store, state) = await FlowAsync();
        var proposal = new SpecOutcome(Draft("p9001"));
        await store.SetProposalAsync(Platform, Thread, proposal, CancellationToken.None);

        var act = () => flow.ApplyAsync(
            state, proposal, new UnknownConfirmation(), false, CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*UnknownConfirmation*");
        (await store.ReadAsync(Platform, Thread, CancellationToken.None)).Proposal.Should().NotBeNull(
            "a result nobody handled must not take the proposal down with it");
    }

    /// <summary>
    /// 2026-09-25-8e51e: the amendment rides beside the ladder, and the ladder is untouched. A
    /// picked SHAPE is fed into the edit door — the master re-proposes in that shape — and an
    /// amendment is not a re-proposal, so it must not change what a size means.
    /// </summary>
    [Fact]
    public void OutcomeShapes_TheSizeLadder_IsUnchangedForEveryExistingKind()
    {
        OutcomeShapes.For(new SpecOutcome(Draft("p9001"))).Select(s => s.Label)
            .Should().Equal("Cut into several specs", "Make it a bug ticket");
        OutcomeShapes.For(new EpicOutcome(Draft("p9000"), [Draft("p9000a")])).Select(s => s.Label)
            .Should().Equal("Make it one spec");
        OutcomeShapes.For(new BugOutcome(new BugTicketDraft("t", "d", null))).Select(s => s.Label)
            .Should().Equal("Make it a spec");
    }

    /// <summary>
    /// 2026-10-13-9a59l: the amendment shape is retired — approving in a ticket's own conversation is
    /// what continues the ticket, so a bound conversation is offered exactly the size ladder.
    /// </summary>
    [Fact]
    public async Task OutcomeShapes_BoundConversation_OffersNoAmendShape()
    {
        var spec = await AskedAsync(new SpecOutcome(Draft("p9001")), bound: true);
        var epic = await AskedAsync(new EpicOutcome(Draft("p9000"), [Draft("p9000a")]), bound: true);

        spec.Choices!.Select(c => c.Label).Should().Equal("Cut into several specs", "Make it a bug ticket");
        epic.Choices!.Select(c => c.Label).Should().Equal("Make it one spec");
        OutcomeShapes.All.Should().NotContain(c => c.Label == "Amend this ticket");
    }

    /// <summary>2026-10-13-9a59l: the retired label typed in a bound thread is an ordinary edit note —
    /// it sends the master round again, it never writes the ticket.</summary>
    [Fact]
    public async Task Confirmation_AmendLabelInBoundThread_IsEditNote()
    {
        (await ConfirmAsync(new SpecOutcome(Draft("p9001")), "Amend this ticket", bound: true))
            .Should().BeOfType<OutcomeEditRequested>().Which.Note.Should().Be("Amend this ticket");
        (await ConfirmAsync(new SpecOutcome(Draft("p9001")), "approve", bound: true))
            .Should().BeOfType<OutcomeConfirmed>("approving in a bound thread is the continuation");
    }

    /// <summary>A result added later, standing in for the one nobody remembered to handle.</summary>
    private sealed record UnknownConfirmation : ConfirmationResult;

    private static IEnumerable<OutcomeProposal> EveryConfirmedKind() =>
    [
        new BugOutcome(new BugTicketDraft("t", "d", null)),
        new SpecOutcome(Draft("p9001")),
        new EpicOutcome(Draft("p9000"), [Draft("p9000a")]),
    ];

    /// <summary>The question the confirmer actually asked, captured while the wait is open.</summary>
    private static async Task<DialogQuestion> AskedAsync(OutcomeProposal proposal, bool bound = false)
    {
        var (_, asked) = await RunConfirmerAsync(proposal, "approve", bound);
        return asked!;
    }

    private static async Task<ConfirmationResult> ConfirmAsync(
        OutcomeProposal proposal, string reply, bool bound = false)
    {
        var (result, _) = await RunConfirmerAsync(proposal, reply, bound);
        return result;
    }

    private static async Task<(ConfirmationResult Result, DialogQuestion? Asked)> RunConfirmerAsync(
        OutcomeProposal proposal, string reply, bool bound = false)
    {
        var pending = new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System));
        DialogQuestion? asked = null;
        var transport = new Mock<IDialogueTransport>();
        transport
            .Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string jobId, string questionId, TimeSpan _, CancellationToken _) =>
            {
                asked = pending.TryPeek(jobId, out var peeked) ? peeked.Question : null;
                return new DialogAnswer(questionId, reply, null, DateTimeOffset.UtcNow, "U1");
            });
        var confirmer = new SpecDialogOutcomeConfirmer(
            transport.Object,
            new SpecDialogMessenger([], NullLogger<SpecDialogMessenger>.Instance),
            pending, new SpecDialogOutcomeComposer(),
            NullLogger<SpecDialogOutcomeConfirmer>.Instance);

        var result = await confirmer.ConfirmAsync(State(bound), proposal, CancellationToken.None);
        return (result, asked);
    }

    private async Task<(SpecDialogOutcomeFlow Flow, SpecDialogLatestOutcomeStore Store, ConversationState State)>
        FlowAsync(IOutcomeSink? sink = null)
    {
        var repository = new SpecDialogSessionRepository(_context);
        var sessions = new SpecDialogSessionManager(
            repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(repository), AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
        var state = await sessions.OpenAsync(
            Platform, "C1", Thread, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        var store = new SpecDialogLatestOutcomeStore(
            repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        var messenger = new SpecDialogMessenger([], NullLogger<SpecDialogMessenger>.Instance);
        var composer = new SpecDialogOutcomeComposer();
        var flow = new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                Mock.Of<IDialogueTransport>(), messenger,
                new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
                composer, NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            sink ?? Mock.Of<IOutcomeSink>(), composer, messenger,
            new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance),
            store, NullLogger<SpecDialogOutcomeFlow>.Instance);
        return (flow, store, state);
    }

    private static ConversationState State(bool bound = false) => new()
    {
        Tracker = bound ? "sample-tracker" : null,
        TicketKey = bound ? "github-4711" : null,
        JobId = "sess-355b",
        ChannelId = "C1",
        ThreadId = Thread,
        UserId = "U1",
        Platform = Platform,
        Project = "sample",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
    };

    private static SpecDraft Draft(string specId) =>
        new(specId, $"goal of {specId}", $"spec: {specId}", []);
}

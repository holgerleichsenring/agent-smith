using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-15-6d9c: what the dashboard's second pane is given. The turn's typed outcome is
/// published from the outcome flow — which already receives it — before the person is
/// asked to approve it, and what the filing attempt actually created is published from the
/// sink that holds the report. Both reach the one session group and nothing wider.
/// </summary>
public sealed class DialogProposalPaneTests : IDisposable
{
    private const string Dialog = "d-6d9c";

    private readonly RecordingDialogHub _hub = new();
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public DialogProposalPaneTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Flow_WithAPhaseProposal_PublishesTheTypedProposal()
    {
        var flow = Flow(new OutcomeRejected());

        await flow.HandleAsync(State(), new PhaseOutcome(Draft("p9001")), false, CancellationToken.None);

        var push = Proposals().Single();
        push.Kind.Should().Be(SpecDialogProposalComposer.PhaseKind);
        push.Phase!.PhaseId.Should().Be("p9001");
        push.Phase.Goal.Should().Be("goal of p9001");
        push.Phase.Steps.Should().Equal("open the seam");
        push.Phase.Tests.Should().Equal("Flow_Scenario_Expected");
        push.Phase.Done.Should().Equal("the pane shows it");
    }

    [Fact]
    public async Task Flow_WithAnAnswerOutcome_PublishesNoProposal()
    {
        var flow = Flow(new OutcomeRejected());

        await flow.HandleAsync(State(), new AnswerOutcome(), false, CancellationToken.None);

        Proposals().Should().BeEmpty(
            "an answer proposes nothing, so the pane keeps whatever is still under discussion");
    }

    [Fact]
    public async Task Flow_AfterAnEditNote_PublishesTheSupersedingProposal()
    {
        var flow = Flow(new OutcomeEditRequested("cut it smaller"), new OutcomeRejected());
        var state = State();

        var first = await flow.HandleAsync(
            state, new PhaseOutcome(Draft("p9001")), false, CancellationToken.None);
        await flow.HandleAsync(state, new PhaseOutcome(Draft("p9002")), false, CancellationToken.None);

        first.Should().BeOfType<OutcomeFlowEditRequested>();
        Proposals().Select(push => push.Phase!.PhaseId).Should().Equal(["p9001", "p9002"],
            "the edit round sends the design turn round again and the pane follows it");
    }

    [Fact]
    public async Task Proposal_ReachesOnlyTheSessionGroup()
    {
        var flow = Flow(new OutcomeRejected());

        await flow.HandleAsync(State(), new PhaseOutcome(Draft("p9001")), false, CancellationToken.None);

        _hub.Pushes.Should().OnlyContain(push => push.Group == HubGroups.SpecDialog(Dialog),
            "a design conversation is addressed to the one person holding it");
    }

    [Fact]
    public async Task Propose_OnAChatPlatform_PublishesNothingToTheHub()
    {
        var channel = Channel();

        await channel.ProposeAsync(
            State() with { Platform = "slack" }, new PhaseOutcome(Draft("p9001")),
            CancellationToken.None);

        _hub.Pushes.Should().BeEmpty(
            "a Slack thread reads its proposal in the thread, and its id is a dialog id "
            + "nobody has joined");
    }

    [Fact]
    public void Compose_AnEpic_OrdersTheChildrenTheWayTheFilerWillFileThem()
    {
        var epic = new EpicOutcome(
            Draft("p9000"), [Draft("p9000a", requires: ["p9000b"]), Draft("p9000b")]);

        var push = Composer().Compose(Dialog, epic, DateTimeOffset.UnixEpoch)!;

        push.Kind.Should().Be(SpecDialogProposalComposer.EpicKind);
        push.Parent!.PhaseId.Should().Be("p9000");
        push.Children.Select(child => child.PhaseId).Should().Equal(["p9000b", "p9000a"],
            "the cut listed them the other way round; the filer follows the edges, and a "
            + "pane listing them differently shows a plan that is not the one about to be created");
    }

    [Fact]
    public void Compose_ABugOutcome_CarriesTheBodyTheFilerWouldFile()
    {
        var bug = new BugOutcome(new BugTicketDraft("Widget drops", "It drops.", "It stops dropping."));

        var push = Composer().Compose(Dialog, bug, DateTimeOffset.UnixEpoch)!;

        push.Kind.Should().Be(SpecDialogProposalComposer.BugKind);
        push.Bug!.Title.Should().Be("Widget drops");
        push.Bug.Body.Should().Be(new BugTicketRenderer().RenderBody(
            new BugTicketDraft("Widget drops", "It drops.", "It stops dropping.")));
    }

    [Fact]
    public void Compose_AnAnswerOutcome_ComposesNothing() =>
        Composer().Compose(Dialog, new AnswerOutcome(), DateTimeOffset.UnixEpoch).Should().BeNull();

    [Fact]
    public async Task Filed_AfterAPartialFailure_PublishesWhatWasCreatedAndTheError()
    {
        var report = new FilingReport(
            [new FiledTicket("https://tracker/1", "p9000: the cut")], "the tracker refused the child");

        await Channel().FiledAsync(State(), report, CancellationToken.None);

        var push = Filings().Single();
        push.Filed.Should().Equal([new FiledTicket("https://tracker/1", "p9000: the cut")],
            "a partial epic must never silently lose the children it did create");
        push.Error.Should().Be("the tracker refused the child");
    }

    /// <summary>2026-09-17-042ea: a child the tracker would not link is said, not hidden.</summary>
    [Fact]
    public async Task Filed_WithANote_PublishesTheNoteAndNamesItInTheNotice()
    {
        var report = new FilingReport([new FiledTicket("https://tracker/2", "p9000a: slice")], Error: null)
        {
            Notes = ["https://tracker/2 is not linked to its parent https://tracker/1: refused"],
        };

        await Channel().FiledAsync(State(), report, CancellationToken.None);

        Filings().Single().Notes.Should().Equal(report.Notes);
        new SpecDialogOutcomeComposer().ComposeFiled(new PhaseOutcome(new PhaseDraft("p9000a", "slice", "phase: p9000a", [])), report)
            .In(SpecDialogMarkup.For("slack")).Should().Contain("is not linked to its parent");
    }

    [Fact]
    public async Task Filed_AfterASuccessfulFiling_PublishesTheReferencesAndTitles()
    {
        var report = new FilingReport(
            [new FiledTicket("https://tracker/7", "p9001: the phase")], Error: null);

        await Channel().FiledAsync(State(), report, CancellationToken.None);

        var push = Filings().Single();
        push.Error.Should().BeNull();
        push.Filed.Single().Reference.Should().Be("https://tracker/7");
        push.Filed.Single().Title.Should().Be("p9001: the phase");
    }

    [Fact]
    public void RenderBody_WithAcceptanceCriteria_PutsThemUnderTheirOwnHeading() =>
        new BugTicketRenderer()
            .RenderBody(new BugTicketDraft("t", "It drops.", "It stops dropping."))
            .Should().Be("It drops.\n\n## Acceptance criteria\nIt stops dropping.");

    [Fact]
    public void RenderBody_WithoutAcceptanceCriteria_IsTheDescriptionAlone() =>
        new BugTicketRenderer()
            .RenderBody(new BugTicketDraft("t", "It drops.", null))
            .Should().Be("It drops.");

    private IEnumerable<SpecDialogProposalPush> Proposals() =>
        _hub.Pushes.Where(push => push.Method == "SpecDialogProposal")
            .Select(push => (SpecDialogProposalPush)push.Args[0]!);

    private IEnumerable<SpecDialogFilingPush> Filings() =>
        _hub.Pushes.Where(push => push.Method == "SpecDialogFiled")
            .Select(push => (SpecDialogFilingPush)push.Args[0]!);

    private static SpecDialogProposalComposer Composer() =>
        new(new EpicChildOrderer(), new BugTicketRenderer());

    private DashboardOutcomeChannel Channel() =>
        new(Composer(), NullLogger<DashboardOutcomeChannel>.Instance, _hub);

    /// <summary>
    /// The real flow over a confirmer whose answers are scripted: the proposal is published
    /// before the gate is asked, so what the gate then answers only decides where the flow
    /// goes next — never whether the pane was told.
    /// </summary>
    private SpecDialogOutcomeFlow Flow(params ConfirmationResult[] answers)
    {
        var transport = new Mock<IDialogueTransport>();
        var queue = new Queue<ConfirmationResult>(answers);
        transport
            .Setup(t => t.WaitForAnswerAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<TimeSpan>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((string _, string questionId, TimeSpan _, CancellationToken _) =>
                Answer(questionId, queue.Dequeue()));
        var messenger = new SpecDialogMessenger([], NullLogger<SpecDialogMessenger>.Instance);
        var composer = new SpecDialogOutcomeComposer();
        return new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(
                transport.Object, messenger, new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)), composer,
                NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            Mock.Of<IOutcomeSink>(), composer, messenger, Channel(),
            new SpecDialogLatestOutcomeStore(new SpecDialogSessionRepository(_context), Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<SpecDialogOutcomeFlow>.Instance);
    }

    private static DialogAnswer Answer(string questionId, ConfirmationResult scripted) =>
        new(questionId,
            scripted switch
            {
                OutcomeConfirmed => "approve",
                OutcomeRejected => "reject",
                OutcomeEditRequested edit => edit.Note,
                _ => string.Empty,
            },
            null, DateTimeOffset.UtcNow, "U1");

    private static ConversationState State() => new()
    {
        JobId = "job-1",
        ChannelId = Dialog,
        ThreadId = Dialog,
        UserId = "U1",
        Platform = "dashboard",
        Project = "sample",
        TicketId = string.Empty,
        StartedAt = DateTimeOffset.UtcNow,
        Mode = ConversationMode.SpecDialog,
    };

    private static PhaseDraft Draft(string phaseId, IReadOnlyList<string>? requires = null) =>
        new(phaseId, $"goal of {phaseId}", $"phase: {phaseId}", requires ?? [])
        {
            Steps = [new PhaseStep("seam", "open the seam", null)],
            Tests = ["Flow_Scenario_Expected"],
            Done = ["the pane shows it"],
        };
}

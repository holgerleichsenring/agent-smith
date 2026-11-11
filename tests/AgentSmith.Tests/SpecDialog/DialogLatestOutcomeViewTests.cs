using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// What a page reloaded mid-conversation is served: the turns as a person reads them, and the
/// proposal pane's state kept on the session — the proposal under discussion with its raw form,
/// the latest filing, and the turn the card belongs on. The REAL outcome flow and filing sink
/// write it over the REAL durable store; the approval answer and the tracker are scripted.
/// </summary>
public sealed class DialogLatestOutcomeViewTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Dialog = "d-c7aea";
    private const string Owner = "person-a";
    private const string DraftYaml = "spec: p9999\ngoal: \"widget goal\"";
    private const string Draft = "```yaml\n" + DraftYaml + "\n```";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly RecordingDialogHub _hub = new();

    public DialogLatestOutcomeViewTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(_repository), AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System,
            NullLogger<SpecDialogSessionManager>.Instance);
    }

    [Fact]
    public async Task View_AnAssistantTurnWithADraft_ShowsItsProseWithoutIt()
    {
        await ConversationAsync((TranscriptRole.Assistant, "Here is the spec.\n\n" + Draft + "\n\nApprove it?"));

        var view = await ReadAsync();

        view.Transcript.Single().Text.Should().Be("Here is the spec.\n\nApprove it?");
    }

    [Fact]
    public async Task View_AnOperatorTurnWithAPastedDraft_ShowsItAsWritten()
    {
        var pasted = "Start from this:\n" + Draft;
        await ConversationAsync((TranscriptRole.User, pasted));

        (await ReadAsync()).Transcript.Single().Text.Should().Be(pasted);
    }

    [Fact]
    public async Task View_DraftOnlyReply_ShowsNoProseLine()
    {
        await ConversationAsync((TranscriptRole.Assistant, Draft));

        (await ReadAsync()).Transcript.Single().Text.Should().BeEmpty("the card carries the draft");
    }

    [Fact]
    public async Task View_AfterFilingSucceeds_CarriesTheFilingAndNoAwaitingProposal()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));

        await DecideAsync(state, Proposal(), "approve");

        var view = await ReadAsync();
        view.Proposal.Should().BeNull("2026-11-10-ccd9: the claim took it out of the awaiting slot — nothing is offered twice");
        view.Filing!.Error.Should().BeNull();
        view.Filing.Filed.Single().Reference.Should().Be("https://tracker.test/1");
    }

    /// <summary>2026-09-17-042ea: a filing note survives a reload like the tickets it is about.</summary>
    [Fact]
    public async Task View_AFilingWithANote_KeepsTheNote()
    {
        await ConversationAsync((TranscriptRole.Assistant, Draft));
        var store = new SpecDialogLatestOutcomeStore(_repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(_repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance);

        await store.SetFilingAsync(Platform, Dialog,
            new FilingReport([new("https://tracker.test/2", "p9999: widget goal")], null)
            {
                Notes = ["https://tracker.test/2 is not linked to its parent https://tracker.test/1: refused"],
            },
            Proposal(), CancellationToken.None);

        (await ReadAsync()).Filing!.Notes.Should()
            .Equal("https://tracker.test/2 is not linked to its parent https://tracker.test/1: refused");
    }

    [Fact]
    public async Task View_AfterARejection_CarriesNoLatestProposal()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));

        await DecideAsync(state, Proposal(), "reject");

        var view = await ReadAsync();
        view.Proposal.Should().BeNull("a reload must not offer what the operator turned down");
        view.ProposalTurn.Should().BeNull();
    }

    [Fact]
    public async Task View_PlacesTheCardOnTheLastAssistantTurnWithADraft()
    {
        var state = await ConversationAsync(
            (TranscriptRole.User, "a widget"), (TranscriptRole.Assistant, "First cut:\n" + Draft),
            (TranscriptRole.User, "smaller"), (TranscriptRole.Assistant, "Second cut:\n" + Draft),
            (TranscriptRole.User, "why?"), (TranscriptRole.Assistant, "Because the store is shared."));

        // Set directly: this is about where the card goes.
        await Harness().Proposals.StoreAwaitingAsync(state, Proposal(), CancellationToken.None);

        (await ReadAsync()).ProposalTurn.Should().Be(3);
    }

    /// <summary>2026-11-10-ccd9: nothing expires — a proposal nobody answered is shown from the row after a reload,
    /// with the id an approve of it carries, and its approval card is rebuilt from the row.</summary>
    [Fact]
    public async Task ViewReader_AfterRestart_ShowsTheCardFromTheRow()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));

        await Flow().HandleAsync(state, Proposal(), CancellationToken.None);

        var view = await ReadViewAsync();
        view.Session!.Proposal!.ProposalId.Should().NotBeNullOrEmpty();
        view.Question!.QuestionId.Should().Be(view.Session.Proposal.ProposalId, "the card names the proposal it shows");
        view.Question.ExpiresAt.Should().BeNull("a proposal never expires");
    }

    /// <summary>
    /// The filing record is written AFTER the tickets exist. Had a failed save propagated, the
    /// notice telling the master and the transcript what was filed would never be written, and an
    /// operator seeing no confirmation files everything a second time.
    /// </summary>
    [Fact]
    public async Task LatestOutcomeStore_WhenTheSaveFails_DoesNotFailTheCaller()
    {
        var broken = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        var store = new SpecDialogLatestOutcomeStore(new SpecDialogSessionRepository(broken), AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(new SpecDialogSessionRepository(broken)), Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance);
        await broken.DisposeAsync();

        var write = () => store.SetFilingAsync(
            Platform, Dialog, new FilingReport([], null), Proposal(), CancellationToken.None);

        await write.Should().NotThrowAsync("a display record is not worth a duplicate ticket");
    }

    [Fact]
    public async Task LatestOutcomeStore_AnUnreadableRow_ReadsAsAbsentInsteadOfFailingTheView()
    {
        await ConversationAsync((TranscriptRole.Assistant, Draft));
        var row = await _context.Set<AgentSmith.Infrastructure.Persistence.Entities.SpecDialogSession>()
            .SingleAsync(session => session.ThreadId == Dialog);
        row.LatestProposalJson = "{ not a proposal";
        row.LatestFilingJson = "[1,2,3]";
        await _context.SaveChangesAsync();

        var view = await ReadAsync();

        view.Proposal.Should().BeNull();
        view.Filing.Should().BeNull();
        view.Transcript.Should().NotBeEmpty("the rest of the dialog still reads");
    }

    /// <summary>2026-11-10-ccd9: the awaiting write fails its caller, so no card is shown that the row does not hold.</summary>
    [Fact]
    public async Task OutcomeFlow_AwaitingWriteFails_TurnFailedAndNoCard()
    {
        var state = (await ConversationAsync((TranscriptRole.Assistant, Draft))) with { ThreadId = "d-nobody" };

        await Flow().HandleAsync(state, Proposal(), CancellationToken.None);

        _hub.Pushes.Select(p => p.Args[0]).OfType<SpecDialogProposalPush>().Should().BeEmpty("nothing was stored");
        _hub.Pushes.Select(p => p.Args[0]).OfType<SpecDialogChannelQuestion>().Should().BeEmpty();
    }

    [Fact]
    public async Task StewardTakeover_ClearsAStoredProposal()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));
        await Harness().Proposals.StoreAwaitingAsync(state, Proposal(), CancellationToken.None);

        await Flow().HandleAsync(state, new AnswerOutcome(), CancellationToken.None, stewarded: true);

        (await ReadAsync()).Proposal.Should().BeNull("the steward changes the series with edit_series, not a stale card");
    }

    [Fact]
    public async Task DesignPartnerAnswer_KeepsAStoredProposal()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));
        await Harness().Proposals.StoreAwaitingAsync(state, Proposal(), CancellationToken.None);

        await Flow().HandleAsync(state, new AnswerOutcome(), CancellationToken.None);

        (await ReadAsync()).Proposal.Should().NotBeNull("an answer leaves the proposal under discussion where it was");
    }

    // 2026-09-17-042ed: the findings ride the proposal, so the confirmation the operator approves,
    // the pane push and a reload after it all carry what the turn's own review found.
    [Fact]
    public async Task Flow_Findings_ReachTheConfirmationAndThePush()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));

        await DecideAsync(state, Reviewed(), "reject");

        // 2026-09-17-042ek: on a PAGE the findings reach the operator on the push, which the
        // approval card lists them from — so the question text beside that card is empty. That
        // the sentence still exists in chat is
        // SpecDialogDashboardWordingTests.OutcomeConfirmation_BoundForSlack_ListsTheReviewsFindings,
        // where no flow has to run to prove it; this one stays about what the FLOW published.
        var question = _hub.Pushes.Select(p => p.Args[0]).OfType<SpecDialogChannelQuestion>().Single();
        question.Text.Should().BeEmpty();
        var push = _hub.Pushes.Select(p => p.Args[0]).OfType<SpecDialogProposalPush>().Single();
        push.Findings.Single().Evidence.Should().Contain("[P2]");
    }

    [Fact]
    public async Task View_AfterAReload_CarriesTheFindings()
    {
        var state = await ConversationAsync((TranscriptRole.Assistant, Draft));

        await DecideAsync(state, Reviewed(), "split it");

        var finding = (await ReadAsync()).Proposal!.Findings.Should().ContainSingle().Subject;
        finding.SpecId.Should().Be("p9999");
        finding.Evidence.Should().Contain("the proposal review ran");
        finding.Quote.Should().BeNull("a false premise cites a look, it quotes nothing");
    }

    [Fact]
    public async Task OutcomeProposalJson_RowWithoutFindings_ReadsAsNone()
    {
        await ConversationAsync((TranscriptRole.Assistant, Draft));
        // A row as releases before this one wrote it: a kind and its payload, no findings field.
        var session = await _repository.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None);
        session!.LatestProposalJson =
            "{\"kind\":\"spec\",\"spec\":{\"specId\":\"p9999\",\"goal\":\"widget goal\","
            + "\"yaml\":\"spec: p9999\",\"requires\":[]}}";
        await _repository.SaveAsync(CancellationToken.None);

        (await ReadAsync()).Proposal!.Findings.Should().BeEmpty(
            "a row nobody reviewed is not a review that found nothing wrong");
    }

    private static SpecOutcome Reviewed() => Proposal() with
    {
        Findings =
        [
            new ProposalFinding(
                "p9999", "false premise", "the endpoint is already there", Quote: null,
                Evidence: "[P2] repo-a: the proposal review ran 'read src/Api.cs' exited 0"),
        ],
    };

    private async Task<ConversationState> ConversationAsync(params (TranscriptRole Role, string Text)[] turns)
    {
        await _sessions.OpenAsync(Platform, Dialog, Dialog, Owner,
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);
        foreach (var (role, text) in turns)
            await _sessions.AppendTurnAsync(Platform, Dialog, role, text, null, null, CancellationToken.None);
        return (await _sessions.GetOpenByThreadAsync(Platform, Dialog, CancellationToken.None))!;
    }

    private async Task<SpecDialogSessionView> ReadAsync() => (await ReadViewAsync()).Session!;

    private Task<SpecDialogView> ReadViewAsync() =>
        Harness().View(_sessions, new SpecDialogProjectCatalog(Loader()), new SpecDialogPendingQuestions(new SpecDialogTurnGate(TimeProvider.System)),
                ProposalComposer(), new SpecDialogTurnGate(TimeProvider.System),
                new SpecDialogTicketTextRepository(_context), TestUploads.Over(_context))
            .ReadAsync(Dialog, CancellationToken.None);

    private static SpecOutcome Proposal() => new(new SpecDraft("p9999", "widget goal", DraftYaml, []));

    // 2026-11-10-ccd9: the flow stores and shows; an answer arrives later as a decision on the shown id.
    private SpecDialogOutcomeFlow Flow() => Harness().Flow();

    private SpecDialogDecisionHarness Harness()
    {
        var messenger = new SpecDialogMessenger(
            [new DashboardAdapter(NullLogger<DashboardAdapter>.Instance, _hub)],
            NullLogger<SpecDialogMessenger>.Instance);
        var composer = new SpecDialogOutcomeComposer();
        var channel = new DashboardOutcomeChannel(
            ProposalComposer(), NullLogger<DashboardOutcomeChannel>.Instance, _hub);
        var latest = new SpecDialogLatestOutcomeStore(_repository, AgentSmith.Tests.TestSupport.BoundSessionNudges.Quiet(_repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance);
        return new SpecDialogDecisionHarness(_context, channel, Sink(messenger, composer, channel, latest), messenger);
    }

    private async Task DecideAsync(ConversationState state, OutcomeProposal proposal, string answer)
    {
        var harness = Harness();
        await harness.Flow().HandleAsync(state, proposal, CancellationToken.None);
        if (SpecDialogAnswerWords.DecisionIn(answer) is not { } decision) return;
        var id = (await harness.Outcomes.ReadAsync(Platform, Dialog, CancellationToken.None)).ProposalId;
        await harness.Decisions().DecideAsync(state, decision, id, false, CancellationToken.None);
    }

    private TicketFilingOutcomeSink Sink(
        SpecDialogMessenger messenger, SpecDialogOutcomeComposer composer,
        DashboardOutcomeChannel channel, SpecDialogLatestOutcomeStore latest)
    {
        var provider = new Mock<ITicketProvider>();
        provider.Setup(p => p.CreateAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CreatedTicket(new TicketId("1"), "https://tracker.test/1"));
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider.Object);
        var filer = new OutcomeTicketFiler(
            LiveConfig.Projects(Loader().LoadConfig(string.Empty)), factory.Object, new SpecTicketRenderer(), new BugTicketRenderer(),
            new EpicChildOrderer(), ApprovedSetDoubles.SeriesFiling(),
            TestSupport.ApprovedSetDoubles.SetFiler(),
            FiledWorkDoubles.Starter(), ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance);
        return new TicketFilingOutcomeSink(
            new SpecDialogOutcomeStore(_repository, NullLogger<SpecDialogOutcomeStore>.Instance),
            filer, new AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig().Filer(), TestSupport.Continuations.Unused(filer), _sessions, messenger, composer, channel, latest,
            NullLogger<TicketFilingOutcomeSink>.Instance);
    }

    private static SpecDialogProposalComposer ProposalComposer() =>
        new(new EpicChildOrderer(), new BugTicketRenderer());

    private static IConfigurationLoader Loader()
    {
        var loader = new Mock<IConfigurationLoader>();
        loader.Setup(l => l.LoadConfig(It.IsAny<string>())).Returns(new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                ["sample"] = new()
                {
                    Name = "sample", Repos = [new RepoConnection { Name = "repo-a" }],
                    Tracker = new TrackerConnection(),
                },
            },
        });
        return loader.Object;
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

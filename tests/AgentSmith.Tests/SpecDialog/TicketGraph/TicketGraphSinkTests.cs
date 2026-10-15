using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using static AgentSmith.Tests.SpecDialog.TicketGraph.TicketGraphRig;

namespace AgentSmith.Tests.SpecDialog.TicketGraph;

/// <summary>
/// 2026-10-14-dbcel: the sink files an approved list through the graph filer over the REAL durable store — and refuses
/// one in a ticket's own conversation; once a ticket exists, the stored filing, the notice and the turn are kept even
/// when the caller's token is cancelled. And the texts the notice is composed of.
/// </summary>
public sealed class TicketGraphSinkTests : IDisposable
{
    private const string Platform = "slack";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly TicketGraphRig _rig = new();

    public TicketGraphSinkTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        MigratedStoreTemplate.CopyInto(_context);
        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(_repository, BoundSessionNudges.Quiet(_repository),
            AgentSmith.Tests.Sandbox.Holds.None(), TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
    }

    [Fact]
    public async Task TicketFilingOutcomeSink_GraphInBoundConversation_FilesNothingAndSaysWhere()
    {
        var state = await OpenAsync("th-1") with { TicketKey = "github-12" };

        await Sink().AcceptAsync(state, List(Ticket("api"), Ticket("ui", "api")), true, CancellationToken.None);

        _rig.Tracker.Created.Should().BeEmpty();
        _adapter.Verify(a => a.SendInfoAsync(state.ChannelId, It.IsAny<string>(), It.Is<string>(t =>
            t.Contains("This conversation belongs to github-12; approving here continues that ticket.")
            && t.Contains("File a list of tickets from Work it out without a ticket, or make this one ticket's series.")),
            "th-1", It.IsAny<CancellationToken>()), Times.Once);
        var session = await _repository.GetOpenByThreadAsync(Platform, "th-1", CancellationToken.None);
        session!.ConfirmedOutcomeJson.Should().BeNull("nothing was confirmed for filing");
        SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).Should()
            .Contain(t => t.Kind == SpecDialogTurnKind.Notice && t.Text.Contains("belongs to github-12"));
    }

    [Fact]
    public async Task TicketFilingOutcomeSink_GraphCancelledAfterCreation_StoresTheFilingAndSendsTheNotice()
    {
        var state = await OpenAsync("th-2");
        using var cancel = new CancellationTokenSource();
        _rig.Tracker.AfterCreate = count => { if (count == 1) cancel.Cancel(); };

        await Sink().AcceptAsync(state, List(Ticket("api"), Ticket("ui", "api")), true, cancel.Token);

        _adapter.Verify(a => a.SendInfoAsync(state.ChannelId, It.IsAny<string>(), It.Is<string>(t =>
            t.Contains("interrupted after 1 of 2 tickets") && t.Contains("Approving again files the whole list again")),
            "th-2", It.IsAny<CancellationToken>()), Times.Once);
        var session = await _repository.GetOpenByThreadAsync(Platform, "th-2", CancellationToken.None);
        session!.LatestFilingJson.Should().Contain("tracker.test/101");
        SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson).Should().Contain(t => t.Kind == SpecDialogTurnKind.Filing);
    }

    [Fact]
    public void SpecDialogOutcomeComposer_GraphFiled_SummaryCountsTicketsAndHeldNotesShown()
    {
        var report = new FilingReport(
        [
            new FiledTicket("https://tracker.test/101", "Title api") { Key = "#101", Start = new FiledWorkStart(FiledStartState.Started, "it runs") },
            new FiledTicket("https://tracker.test/102", "Title ui")
                { Key = "#102", Start = new FiledWorkStart(FiledStartState.Held, "waits for #101 to be merged or marked done by hand") },
        ], Error: null);

        var notice = new SpecDialogOutcomeComposer().ComposeFiled(List(Ticket("api"), Ticket("ui", "api")), report)
            .In(SpecDialogMarkup.CommonMark);

        notice.Should().StartWith("Filed a list of 2 tickets, in the order they must merge:")
            .And.Contain("  - held: waits for #101 to be merged or marked done by hand");
    }

    [Fact]
    public void SpecDialogOutcomeComposer_GraphFilingFailure_SaysNotToRetryWhole()
    {
        var report = new FilingReport([new FiledTicket("https://tracker.test/101", "Title api") { Key = "#101" }], "the tracker refused the create")
            { Notes = ["Not created: ui — Title ui."] };

        var notice = new SpecDialogOutcomeComposer().ComposeFilingFailure(report, List(Ticket("api"), Ticket("ui", "api")))
            .In(SpecDialogMarkup.CommonMark);

        notice.Should().Contain("Not created: ui — Title ui.")
            .And.EndWith("Approving again files the whole list again: close the created tickets, or propose the rest naming them with `ticket:<id>`.")
            .And.NotContain("retry");
    }

    [Fact]
    public void SpecDialogOutcomeComposer_GraphRefusedBeforeCreation_KeepsTheRetryLine()
    {
        var report = new FilingReport([], "Nothing was filed: ticket 42 does not exist on sample-tracker.");

        var notice = new SpecDialogOutcomeComposer().ComposeFilingFailure(report, List(Ticket("api", "ticket:42"), Ticket("ui")))
            .In(SpecDialogMarkup.CommonMark);

        notice.Should().StartWith("Ticket filing failed — nothing was created.")
            .And.Contain("ticket 42 does not exist").And.EndWith("to re-propose and retry.");
    }

    private TicketFilingOutcomeSink Sink()
    {
        var messenger = new SpecDialogMessenger([_adapter.Object], NullLogger<SpecDialogMessenger>.Instance);
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(_rig.Tracker);
        var filer = new OutcomeTicketFiler(_rig.Config(), factory.Object, new SpecTicketRenderer(), new BugTicketRenderer(),
            new EpicChildOrderer(), ApprovedSetDoubles.SeriesFiling(), ApprovedSetDoubles.SetFiler(),
            FiledWorkDoubles.Starter(), ApprovedSetDoubles.Kinds(), NullLogger<OutcomeTicketFiler>.Instance);
        return new TicketFilingOutcomeSink(
            new SpecDialogOutcomeStore(_repository, NullLogger<SpecDialogOutcomeStore>.Instance),
            filer, _rig.Filer(), Continuations.Unused(filer), _sessions, messenger, new SpecDialogOutcomeComposer(),
            new DashboardOutcomeChannel(new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance),
            new SpecDialogLatestOutcomeStore(_repository, BoundSessionNudges.Quiet(_repository), NullLogger<SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<TicketFilingOutcomeSink>.Instance);
    }

    private async Task<ConversationState> OpenAsync(string threadId) =>
        await _sessions.OpenAsync(Platform, "C1", threadId, "U1",
            new ActiveScope { Project = "proj", Repos = ["sample-api"] }, CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

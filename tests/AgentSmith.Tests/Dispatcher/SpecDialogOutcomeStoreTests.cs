using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
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

using AgentSmith.Tests.TestSupport;

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// p0315e/p0315c: the ticket-filing outcome sink over the REAL durable store.
/// Durable-first: the confirmed proposal is persisted on the SpecDialogSession
/// BEFORE filing; a tracker failure keeps it (round-trippable JSON) for a
/// retry and reports honestly, a full success clears it and posts the
/// references to the thread and the dialogue trail.
/// </summary>
public sealed class SpecDialogOutcomeStoreTests : IDisposable
{
    private const string Platform = "slack";
    private const string Project = "sample";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly Mock<IPlatformAdapter> _adapter = new();

    public SpecDialogOutcomeStoreTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();

        _adapter.SetupGet(a => a.Platform).Returns(Platform);
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, TimeProvider.System, NullLogger<SpecDialogSessionManager>.Instance);
    }

    [Fact]
    public async Task AcceptAsync_FilingSucceeds_ClearsStoredOutcomeAndPostsReferences()
    {
        var provider = new RecordingProvider();
        var sink = BuildSink(provider);
        var state = await OpenSessionAsync("th-1");
        var phase = new PhaseOutcome(new PhaseDraft(
            "p9999", "Widget endpoint", "phase: p9999\ngoal: \"Widget endpoint\"", []));

        await sink.AcceptAsync(state, phase, false, CancellationToken.None);

        provider.Created.Should().ContainSingle().Which.Title.Should().Be("p9999: Widget endpoint");
        var session = await _repository.GetOpenByThreadAsync(Platform, "th-1", CancellationToken.None);
        session!.ConfirmedOutcomeJson.Should().BeNull(
            "a fully filed outcome is no longer pending on the session");
        _adapter.Verify(a => a.SendInfoAsync(
            state.ChannelId, It.IsAny<string>(),
            It.Is<string>(t => t.Contains("https://tracker.test/1")),
            "th-1", It.IsAny<CancellationToken>()), Times.Once);
        var trail = SpecDialogSessionMapper.ReadTranscript(session.TranscriptJson);
        trail.Should().Contain(t =>
            t.Role == TranscriptRole.Assistant && t.Text.Contains("https://tracker.test/1"),
            "the dialogue trail records what was filed");
    }

    [Fact]
    public async Task AcceptAsync_TrackerCreateFails_KeepsStoredOutcomeAndReportsError()
    {
        var provider = new RecordingProvider { FailWith = "tracker down" };
        var sink = BuildSink(provider);
        var state = await OpenSessionAsync("th-2");
        var bug = new BugOutcome(new BugTicketDraft("Fix the null deref", "AppendTurnAsync NREs.", null));

        await sink.AcceptAsync(state, bug, false, CancellationToken.None);

        var session = await _repository.GetOpenByThreadAsync(Platform, "th-2", CancellationToken.None);
        session!.ConfirmedOutcomeJson.Should().NotBeNull(
            "the confirmed outcome survives a filing failure so it can be retried");
        var restored = OutcomeProposalJson.Read(session.ConfirmedOutcomeJson!);
        restored.Should().BeEquivalentTo(bug, "the retry must read back exactly what was confirmed");
        _adapter.Verify(a => a.SendInfoAsync(
            state.ChannelId, It.IsAny<string>(),
            It.Is<string>(t => t.Contains("failed") && t.Contains("tracker down")),
            "th-2", It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 2026-09-13-ed5a: the turn's scopes are disposed before the outcome flow runs and filing
    /// retries from the stored row in a LATER process, so the provenance has to survive the
    /// column — not a live sandbox. No new column: it rides the outcome JSON that is already
    /// the durable handoff.
    /// </summary>
    [Fact]
    public async Task SpecDialogSession_TemplateRevisions_SurviveAReload()
    {
        var provider = new RecordingProvider { FailWith = "tracker down" };
        var sink = BuildSink(provider);
        var state = await OpenSessionAsync("th-3");
        var epic = new EpicOutcome(
            new PhaseDraft("p9000", "Widget platform", "phase: p9000", []),
            [new PhaseDraft("p9000a", "storage", "phase: p9000a", [])])
        {
            Templates = [new TemplateProvenance("template:default", "reference-server", "a1b2c3d", true)],
        };

        await sink.AcceptAsync(state, epic, false, CancellationToken.None);

        var session = await _repository.GetOpenByThreadAsync(Platform, "th-3", CancellationToken.None);
        var restored = OutcomeProposalJson.Read(session!.ConfirmedOutcomeJson!);
        restored.Templates.Should().ContainSingle().Which.Should().BeEquivalentTo(epic.Templates[0],
            "a retry in another process files the parent that names what the analysis read");
    }

    [Fact]
    public async Task SetConfirmedAsync_NoOpenSession_FailsLoudly()
    {
        var store = new SpecDialogOutcomeStore(_repository, NullLogger<SpecDialogOutcomeStore>.Instance);
        var act = () => store.SetConfirmedAsync(
            Platform, "th-none", new PhaseOutcome(new PhaseDraft("p1", "g", "phase: p1", [])),
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*th-none*");
    }

    private TicketFilingOutcomeSink BuildSink(RecordingProvider provider)
    {
        var messenger = new SpecDialogMessenger(
            [_adapter.Object], NullLogger<SpecDialogMessenger>.Instance);
        var factory = new Mock<ITicketProviderFactory>();
        factory.Setup(f => f.Create(It.IsAny<TrackerConnection>())).Returns(provider);
        var config = new AgentSmithConfig
        {
            Projects = new Dictionary<string, ResolvedProject>
            {
                [Project] = new() { Name = Project, Tracker = new TrackerConnection() },
            },
        };
        var filer = new OutcomeTicketFiler(
            config, factory.Object, new PhaseTicketRenderer(), new BugTicketRenderer(),
            TestSupport.ApprovedSetDoubles.EpicFiler(),
            TestSupport.ApprovedSetDoubles.Recorder(),
            FiledWorkDoubles.Starter(), NullLogger<OutcomeTicketFiler>.Instance);
        return new TicketFilingOutcomeSink(
            new SpecDialogOutcomeStore(_repository, NullLogger<SpecDialogOutcomeStore>.Instance),
            filer, _sessions, messenger, new SpecDialogOutcomeComposer(),
            new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance),
            new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance),
            NullLogger<TicketFilingOutcomeSink>.Instance);
    }

    private async Task<ConversationState> OpenSessionAsync(string threadId) =>
        await _sessions.OpenAsync(
            Platform, "C1", threadId, "U1",
            new ActiveScope { Project = Project, Repos = ["repo-a"] },
            CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class RecordingProvider : ITicketProvider
    {
        private readonly List<(string Title, string Body, IReadOnlyList<string> Labels)> _created = [];
        public IReadOnlyList<(string Title, string Body, IReadOnlyList<string> Labels)> Created => _created;
        public string? FailWith { get; init; }

        public string ProviderType => "recording";

        public Task<ConnectionProbeResult> ProbeAsync(CancellationToken cancellationToken) =>
            Task.FromResult(ConnectionProbeResult.Reachable(0));

        public Task<Ticket> GetTicketAsync(TicketId ticketId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CreatedTicket> CreateAsync(
            string title, string description, IReadOnlyList<string> labels,
            CancellationToken cancellationToken)
        {
            if (FailWith is not null) throw new InvalidOperationException(FailWith);
            _created.Add((title, description, labels));
            return Task.FromResult(new CreatedTicket(
                new TicketId(_created.Count.ToString()),
                $"https://tracker.test/{_created.Count}"));
        }

        public Task<ParentLinkResult> LinkToParentAsync(
            CreatedTicket child, TicketId parent, CancellationToken cancellationToken) =>
            Task.FromResult(ParentLinkResult.Linked);

        public Task<TicketFinalizeResult> FinalizeAsync(
            TicketId ticketId, string comment, string? doneStatus, CancellationToken cancellationToken) =>
            Task.FromResult(TicketFinalizeResult.Moved());
    }
}

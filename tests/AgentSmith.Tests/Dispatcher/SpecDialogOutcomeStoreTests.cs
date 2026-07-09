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

namespace AgentSmith.Tests.Dispatcher;

/// <summary>
/// p0315e: the default outcome sink over the REAL durable store — a confirmed
/// proposal is persisted on the SpecDialogSession (round-trippable JSON, the
/// p0315c filing handoff) and the thread gets an honest "stored, not filed"
/// notice instead of a fake ticket.
/// </summary>
public sealed class SpecDialogOutcomeStoreTests : IDisposable
{
    private const string Platform = "slack";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly Mock<IPlatformAdapter> _adapter = new();
    private readonly SessionStoreOutcomeSink _sink;

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
        _sink = new SessionStoreOutcomeSink(
            new SpecDialogOutcomeStore(_repository, NullLogger<SpecDialogOutcomeStore>.Instance),
            new SpecDialogMessenger([_adapter.Object], NullLogger<SpecDialogMessenger>.Instance),
            new SpecDialogOutcomeComposer(),
            NullLogger<SessionStoreOutcomeSink>.Instance);
    }

    [Fact]
    public async Task AcceptAsync_ConfirmedEpic_PersistsRoundTrippableJsonAndPostsHonestNotice()
    {
        var state = await OpenSessionAsync("th-1");
        var epic = new EpicOutcome(
            new PhaseDraft("p9000", "Widget platform", "phase: p9000\ngoal: \"Widget platform\"", []),
            [
                new PhaseDraft("p9000a", "Storage", "phase: p9000a\ngoal: \"Storage\"", []),
                new PhaseDraft("p9000b", "API", "phase: p9000b\ngoal: \"API\"\nrequires: [p9000a]", ["p9000a"]),
            ]);

        await _sink.AcceptAsync(state, epic, CancellationToken.None);

        var session = await _repository.GetOpenByThreadAsync(Platform, "th-1", CancellationToken.None);
        session!.ConfirmedOutcomeJson.Should().NotBeNull("the confirmed proposal is the durable p0315c handoff");
        var restored = OutcomeProposalJson.Read(session.ConfirmedOutcomeJson!);
        restored.Should().BeOfType<EpicOutcome>();
        restored.Should().BeEquivalentTo(epic, "p0315c must read back exactly what was confirmed");
        _adapter.Verify(a => a.SendInfoAsync(
            state.ChannelId, It.IsAny<string>(),
            It.Is<string>(t => t.Contains("Nothing has been filed yet") && t.Contains("p0315c")),
            "th-1", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AcceptAsync_ConfirmedBug_PersistsFixBugTicketShape()
    {
        var state = await OpenSessionAsync("th-2");
        var bug = new BugOutcome(new BugTicketDraft("Fix the null deref", "AppendTurnAsync NREs.", null));

        await _sink.AcceptAsync(state, bug, CancellationToken.None);

        var session = await _repository.GetOpenByThreadAsync(Platform, "th-2", CancellationToken.None);
        var restored = OutcomeProposalJson.Read(session!.ConfirmedOutcomeJson!);
        restored.Should().BeOfType<BugOutcome>()
            .Which.Ticket.Title.Should().Be("Fix the null deref");
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

    private async Task<ConversationState> OpenSessionAsync(string threadId) =>
        await _sessions.OpenAsync(
            Platform, "C1", threadId, "U1",
            new ActiveScope { Project = "sample", Repos = ["repo-a"] },
            CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

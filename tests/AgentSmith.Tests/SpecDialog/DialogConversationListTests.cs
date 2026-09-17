using System.Security.Claims;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Extensions;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// The caller's conversation list over the REAL durable store (SQLite in-memory with the
/// shipped migrations): closed conversations stay listed, another principal's never are, each
/// is titled by what the person wrote and marked by what its latest filing created.
/// </summary>
public sealed class DialogConversationListTests : IDisposable
{
    private const string Platform = "dashboard";
    private const string Owner = "person-a";
    private const string Intruder = "person-b";

    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;
    private readonly SpecDialogSessionManager _sessions;
    private readonly SteppedClock _clock = new();

    public DialogConversationListTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
        _sessions = new SpecDialogSessionManager(
            _repository, _clock, NullLogger<SpecDialogSessionManager>.Instance);
    }

    [Fact]
    public async Task List_IncludesTheCallersClosedConversations()
    {
        var closed = await OpenAsync("d-1");
        await _sessions.CloseAsync(Platform, "d-1", CancellationToken.None);
        var open = await OpenAsync("d-2");

        var listed = await ListAsync();

        listed.Select(summary => summary.SessionId).Should().BeEquivalentTo([closed, open]);
    }

    [Fact]
    public async Task List_ExcludesAnotherPrincipalsConversations()
    {
        var mine = await OpenAsync("d-1");
        await OpenAsync("d-2", Intruder);

        (await ListAsync()).Should().ContainSingle().Which.SessionId.Should().Be(mine,
            "a session id is all a resume needs, so another principal's may not be listed");
    }

    [Fact]
    public async Task List_IsCappedAndSortedByActivity()
    {
        var created = new List<string>();
        for (var index = 0; index <= SpecDialogConversationList.Cap; index++)
            created.Add(await OpenAsync($"d-{index}"));
        _clock.Advance(TimeSpan.FromMinutes(5));
        await SayAsync("d-3", "back to the third one");

        var listed = await ListAsync();

        listed.Should().HaveCount(SpecDialogConversationList.Cap);
        listed.Select(summary => summary.SessionId).Should().NotContain(created[0],
            "the cap keeps the most recently created");
        listed[0].SessionId.Should().Be(created[3], "the list is sorted by last activity");
    }

    [Fact]
    public async Task ListRoute_AnswersForTheSignedInPrincipalOnly()
    {
        var mine = await OpenAsync("d-1");
        await OpenAsync("d-2", Intruder);

        var result = await SpecDialogViewEndpoints.ListAsync(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", Owner)], "test")),
            new SpecDialogOwnership(_repository, new SpecCommandParser()),
            new SpecDialogConversationList(_repository, new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance)), CancellationToken.None);

        result.Should().BeOfType<Ok<IReadOnlyList<SpecDialogSessionSummary>>>()
            .Which.Value!.Select(summary => summary.SessionId).Should().Equal(mine);
    }

    [Fact]
    public void List_IsNotReadOnEveryDialogViewRefetch()
    {
        typeof(SpecDialogView).GetProperties()
            .Should().NotContain(property =>
                property.PropertyType == typeof(IReadOnlyList<SpecDialogSessionSummary>),
                "the page reads the view after every message; the list has its own read");
    }

    [Fact]
    public async Task Title_IsTheFirstDesignMessagesFirstProseLine()
    {
        await OpenAsync("d-1");
        await SayAsync("d-1", "\n\n  A widget that reads the ledger  \nwith a second line");
        await SayAsync("d-1", "approve");

        (await ListAsync()).Single().Title.Should().Be("A widget that reads the ledger");
    }

    [Fact]
    public async Task Title_SkipsALeadingCodeFence()
    {
        await OpenAsync("d-1");
        await SayAsync("d-1", "```yaml\nphase: p1\n```\n\nStart from this draft");

        (await ListAsync()).Single().Title.Should().Be("Start from this draft");
    }

    [Fact]
    public async Task Title_WithNoDesignMessage_IsNull()
    {
        await OpenAsync("d-1");

        (await ListAsync()).Single().Title.Should().BeNull();
    }

    [Fact]
    public async Task Outcome_DerivedFromTheLatestFiling()
    {
        await OpenAsync("d-1");
        var store = new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance);
        await store.SetProposalAsync(Platform, "d-1", Epic(), CancellationToken.None);
        await store.SetFilingAsync(Platform, "d-1",
            new FilingReport([new("https://tracker.test/1", "p1: parent")], "the second slice was refused"),
            Epic(), CancellationToken.None);
        await _sessions.CloseAsync(Platform, "d-1", CancellationToken.None);

        (await ListAsync()).Single().Outcome.Should().Be(
            new SpecDialogConversationOutcome("epic", Tickets: 1, Partial: true));
    }

    [Fact]
    public async Task Outcome_WithNothingFiled_IsNull()
    {
        await OpenAsync("d-1");
        await new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance)
            .SetProposalAsync(Platform, "d-1", Phase(), CancellationToken.None);

        (await ListAsync()).Single().Outcome.Should().BeNull("only a filing produces an outcome");
    }

    [Fact]
    public async Task Outcome_AnAnswerAfterAFiling_DoesNotChangeIt()
    {
        var state = await StateAsync("d-1");
        var store = new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance);
        await store.SetProposalAsync(Platform, "d-1", Phase(), CancellationToken.None);
        await store.SetFilingAsync(Platform, "d-1",
            new FilingReport([new("https://tracker.test/1", "p1: the phase")], null), Phase(), CancellationToken.None);
        var before = (await ListAsync()).Single().Outcome;

        await Flow(store).HandleAsync(state, new AnswerOutcome(), CancellationToken.None);

        before.Should().Be(new SpecDialogConversationOutcome("phase", Tickets: 1, Partial: false));
        (await ListAsync()).Single().Outcome.Should().Be(before);
    }

    /// <summary>
    /// Found while rebasing this phase onto its predecessor's review fixes. The kind was read back
    /// from the latest PROPOSAL, and that is cleared whenever a later proposal is rejected or times
    /// out — so an epic filed and then followed by a discarded proposal lost what it was, and the
    /// list said "filed" with no kind. The filing now records its own kind.
    /// </summary>
    [Fact]
    public async Task Outcome_AFiledEpicFollowedByADiscardedProposal_StillSaysEpic()
    {
        await OpenAsync("d-1");
        var store = new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance);
        await store.SetProposalAsync(Platform, "d-1", Epic(), CancellationToken.None);
        await store.SetFilingAsync(Platform, "d-1",
            new FilingReport([new("https://tracker.test/1", "p1: parent")], null), Epic(), CancellationToken.None);

        await store.SetProposalAsync(Platform, "d-1", Phase(), CancellationToken.None);
        await store.ClearProposalAsync(Platform, "d-1", CancellationToken.None);

        (await ListAsync()).Single().Outcome!.Kind.Should().Be("epic",
            "what was filed does not change because a later idea was dropped");
    }

    /// <summary>
    /// The list reads every listed session's pane record. One that cannot be read must not take
    /// the whole history down with it.
    /// </summary>
    [Fact]
    public async Task List_AConversationWithAnUnreadableRecord_IsStillListed()
    {
        await OpenAsync("d-1");
        await OpenAsync("d-2");
        var row = await _context.Set<AgentSmith.Infrastructure.Persistence.Entities.SpecDialogSession>()
            .SingleAsync(session => session.ThreadId == "d-1");
        row.LatestFilingJson = "[1,2,3]";
        await _context.SaveChangesAsync();

        var listed = await ListAsync();

        listed.Should().HaveCount(2, "one unreadable record hides only its own outcome");
        listed.Should().OnlyContain(summary => summary.Outcome == null);
    }

    /// <summary>
    /// An open conversation is already somewhere, and the page goes there rather than resuming it:
    /// a resume is refused while its turn runs. So the list says where an open one lives, and says
    /// nothing for a closed one, which has nowhere to go back to.
    /// </summary>
    [Fact]
    public async Task List_AnOpenConversation_NamesTheDialogItLivesOn_AClosedOneDoesNot()
    {
        await OpenAsync("d-open");
        await OpenAsync("d-closed");
        await _sessions.CloseAsync(Platform, "d-closed", CancellationToken.None);
        // Closing is a bulk update this shared context does not track; the list route runs in a
        // request of its own with a fresh context, so it reads what the database holds.
        _context.ChangeTracker.Clear();

        var listed = await ListAsync();

        listed.Single(summary => summary.OpenDialogId == "d-open").Should().NotBeNull();
        listed.Should().ContainSingle(summary => summary.OpenDialogId == null);
    }

    private SpecDialogOutcomeFlow Flow(SpecDialogLatestOutcomeStore store)
    {
        var messenger = new SpecDialogMessenger([], NullLogger<SpecDialogMessenger>.Instance);
        var composer = new SpecDialogOutcomeComposer();
        return new SpecDialogOutcomeFlow(
            new SpecDialogOutcomeConfirmer(Mock.Of<IDialogueTransport>(), messenger,
                new SpecDialogPendingQuestions(), composer, NullLogger<SpecDialogOutcomeConfirmer>.Instance),
            Mock.Of<IOutcomeSink>(), composer, messenger,
            new DashboardOutcomeChannel(
                new SpecDialogProposalComposer(new EpicChildOrderer(), new BugTicketRenderer()),
                NullLogger<DashboardOutcomeChannel>.Instance, new RecordingDialogHub()),
            store, NullLogger<SpecDialogOutcomeFlow>.Instance);
    }

    private static PhaseOutcome Phase() => new(new PhaseDraft("p1", "the phase", "phase: p1", []));

    private static EpicOutcome Epic() => new(
        new PhaseDraft("p1", "parent", "phase: p1", []),
        [new PhaseDraft("p1a", "first", "phase: p1a", []), new PhaseDraft("p1b", "second", "phase: p1b", [])]);

    private async Task<string> OpenAsync(string dialogId, string owner = Owner) =>
        (await StateAsync(dialogId, owner)).JobId;

    private Task<ConversationState> StateAsync(string dialogId, string owner = Owner) =>
        _sessions.OpenAsync(Platform, dialogId, dialogId, owner,
            new ActiveScope { Project = "sample", Repos = ["repo-a"] }, CancellationToken.None);

    private Task SayAsync(string dialogId, string text) =>
        _sessions.AppendTurnAsync(Platform, dialogId, TranscriptRole.User, text, CancellationToken.None);

    private Task<IReadOnlyList<SpecDialogSessionSummary>> ListAsync() =>
        new SpecDialogConversationList(_repository, new SpecDialogLatestOutcomeStore(_repository, Microsoft.Extensions.Logging.Abstractions.NullLogger<AgentSmith.Server.Services.SpecDialog.SpecDialogLatestOutcomeStore>.Instance)).ListAsync(Owner, CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    private sealed class SteppedClock : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.Parse("2026-09-17T08:00:00Z");
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan by) => _now += by;
    }
}

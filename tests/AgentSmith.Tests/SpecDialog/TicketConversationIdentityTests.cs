using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-25-8e51b: one design conversation per ticket, over the real durable store with the
/// shipped migrations.
/// <para>
/// The rule exists because the artifact a ticket conversation amends — the approval record — is
/// keyed by ticket and upserts in place, so two conversations about one ticket are two drafts of
/// one thing with a silent last-writer-wins. The KEY is therefore the record's own spelling, and
/// the unique index is filtered on the one provider that treats two NULLs as equal, or a single
/// ticket-less conversation would occupy the whole table there.
/// </para>
/// </summary>
public sealed class TicketConversationIdentityTests : IDisposable
{
    private const string Tracker = "sample-jira";
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;

    public TicketConversationIdentityTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
    }

    [Fact]
    public void TicketDialog_ATicketIdDifferingOnlyInCase_IsOneConversation()
    {
        var upper = TicketBinding.For(Tracker, "jira", "DPG-1239", "A title");
        var lower = TicketBinding.For(Tracker, "jira", "dpg-1239", "A title");

        lower.Key.Should().Be(upper.Key,
            "the approval record lowercases the id, so two spellings are one record — and two "
            + "conversations over one record is the thing this rule removes");
    }

    [Fact]
    public async Task TicketDialog_TwoTrackersNumberingATicketAlike_AreTwoConversations()
    {
        await AddAsync("a", Tracker, "jira-1");
        await AddAsync("b", "other-jira", "jira-1");

        (await _repository.GetByTicketAsync(Tracker, "jira-1", default))!.SessionId.Should().Be("a");
        (await _repository.GetByTicketAsync("other-jira", "jira-1", default))!.SessionId.Should().Be("b");
    }

    [Fact]
    public async Task TicketDialog_AClosedConversation_IsStillTheTicketsConversation()
    {
        await AddAsync("closed", Tracker, "jira-7", isOpen: false);

        var found = await _repository.GetByTicketAsync(Tracker, "jira-7", default);

        found!.SessionId.Should().Be("closed",
            "an open-only lookup would miss it and insert a duplicate straight into the unique index");
    }

    [Fact]
    public async Task TicketDialog_ASecondConversationForOneTicket_IsRefusedByTheStore()
    {
        await AddAsync("first", Tracker, "jira-9");

        var act = async () => await AddAsync("second", Tracker, "jira-9");

        await act.Should().ThrowAsync<DbUpdateException>(
            "the index is the backstop under the door check — two drafts of one approval record "
            + "must not be reachable even by a race");
    }

    [Fact]
    public async Task TicketDialog_ManyConversationsWithNoTicket_AllInsert()
    {
        await AddAsync("n-1", tracker: null, ticketKey: null);
        await AddAsync("n-2", tracker: null, ticketKey: null);
        await AddAsync("n-3", tracker: null, ticketKey: null);

        (await _repository.GetBySessionIdAsync("n-3", default)).Should().NotBeNull(
            "a unique index over a nullable pair must not make ticket-less conversations exclusive");
    }

    [Fact]
    public void TicketDialog_ATitleCarryingBracketsAndQuotes_IsStillTheHeading()
    {
        var heading = SpecDialogSessionMapper.Heading("""[DPG-1239] "Cannot log in" — 2 users""");

        heading.Should().Be("""[DPG-1239] "Cannot log in" — 2 users""",
            "the admission rule refuses those marks because it judges a MODEL's one-line answer; "
            + "a ticket title is not a guess");
    }

    [Fact]
    public void TicketDialog_ATitleLongerThanTheColumn_IsTrimmedRatherThanDropped()
    {
        var heading = SpecDialogSessionMapper.Heading(new string('x', 400));

        heading.Should().HaveLength(120);
    }

    [Fact]
    public void TicketDialog_ABoundConversation_IsReachableByASecondPrincipal()
    {
        var bound = new SpecDialogSession { UserId = "alice", TicketKey = "jira-1", Tracker = Tracker };

        SpecDialogOwnership.MayReach(bound, "bob").Should().BeTrue(
            "a ticket has ONE conversation because the record it amends is keyed by ticket; "
            + "answering a second principal with a 404 for a ticket they can see on the board is "
            + "the alternative, and it is worse");
    }

    [Fact]
    public void TicketDialog_AConversationWithNoTicket_StaysPrivateToItsOwner()
    {
        var unbound = new SpecDialogSession { UserId = "alice" };

        SpecDialogOwnership.MayReach(unbound, "bob").Should().BeFalse();
        SpecDialogOwnership.MayReach(unbound, "alice").Should().BeTrue();
    }

    [Fact]
    public void TicketDialog_TheKey_IsTheApprovalRecordsOwnSpelling()
    {
        TicketBinding.For(Tracker, "jira", "DPG-1239", "t").Key
            .Should().Be(SpecSetKey.For("jira", "DPG-1239").Value);
    }

    private Task AddAsync(
        string sessionId, string? tracker = null, string? ticketKey = null, bool isOpen = true) =>
        _repository.AddAsync(
            new SpecDialogSession
            {
                SessionId = sessionId,
                Platform = "dashboard",
                ChannelId = sessionId,
                ThreadId = sessionId,
                UserId = "someone",
                Project = "sample",
                Tracker = tracker,
                TicketKey = ticketKey,
                IsOpen = isOpen,
                LastActivityAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

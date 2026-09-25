using AgentSmith.Application.Services.Persistence;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-25-8e51d: a conversation BOUND to a ticket shows the specification a person approved
/// for that ticket, even when the conversation filed nothing itself.
/// <para>
/// The ticket is read off the SESSION ROW and never off a request — the reads on this surface are
/// addressed by dialog id precisely so nobody can follow a ticket by asking for it — and the
/// record is keyed by the conversation's own tracker connection, because the spec key carries
/// only the tracker TYPE.
/// </para>
/// </summary>
public sealed class ApprovedSetInThePaneTests : IDisposable
{
    private const string Dialog = "d-1";
    private const string Tracker = "sample-tracker";
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly InMemorySpecApprovalStore _approvals = new();

    public ApprovedSetInThePaneTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
    }

    [Fact]
    public async Task TicketSpecs_ABoundTicketWithAnApprovedSet_ShowsItsPhasesAndItsApproval()
    {
        await BindAsync("jira-1");
        await _approvals.SaveAsync(
            ApprovedSets.Record("jira-1", ApprovedSets.Noon, ["p0001a"], tracker: Tracker), default);

        var view = await Sut().ForAsync(Dialog, default);

        view.Should().NotBeNull();
        view!.Phases.Should().ContainSingle().Which.PhaseId.Should().Be("p0001a");
        view.ApprovedAt.Should().Be(ApprovedSets.Noon,
            "the pane says whose approval it shows and when — the branch is what a run reads, and "
            + "for a worked ticket the two can differ");
    }

    [Fact]
    public async Task TicketSpecs_ABoundTicketWithNoSet_ShowsNothingRatherThanFailing()
    {
        await BindAsync("jira-2");

        (await Sut().ForAsync(Dialog, default)).Should().BeNull(
            "a hand-written ticket has no approved record, and that is the ordinary case");
    }

    [Fact]
    public async Task TicketSpecs_AConversationWithNoTicket_ShowsNothing()
    {
        await BindAsync(ticketKey: null);
        await _approvals.SaveAsync(
            ApprovedSets.Record("jira-1", ApprovedSets.Noon, tracker: Tracker), default);

        (await Sut().ForAsync(Dialog, default)).Should().BeNull();
    }

    private ApprovedSetForConversation Sut() => new(
        new SpecDialogSessionRepository(_context),
        new SpecDialogTicketTextRepository(_context),
        _approvals,
        new Mock<IConfigurationLoader>().Object);

    private Task BindAsync(string? ticketKey) =>
        new SpecDialogSessionRepository(_context).AddAsync(
            new SpecDialogSession
            {
                SessionId = "s-1",
                Platform = "dashboard",
                ChannelId = Dialog,
                ThreadId = Dialog,
                UserId = "someone",
                Project = "sample",
                Tracker = ticketKey is null ? null : Tracker,
                TicketKey = ticketKey,
                LastActivityAt = DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

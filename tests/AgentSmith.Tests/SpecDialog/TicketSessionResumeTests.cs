using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestSupport;
using AgentSmith.Tests.Tickets;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-10-13-9a59m: a ticket-bound conversation is the ticket's — any caller who may write dialogs resumes
/// it, so a second person can continue the ticket's conversation; one bound to no ticket stays its owner's.
/// </summary>
public sealed class TicketSessionResumeTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly AgentSmithDbContext _context;

    public TicketSessionResumeTests() =>
        _context = new AgentSmithDbContext(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task Resume_TicketBoundSession_AnotherDialogWriter_Resumes()
    {
        _context.Add(TicketConversationFixtures.Session());
        await _context.SaveChangesAsync();

        var result = await Resumer().ResumeAsync("s-1", "second-person", "dashboard", "d-9", "d-9", default);

        result.Should().BeOfType<SpecDialogResumed>().Which.State.ThreadId.Should().Be("d-9");
    }

    [Fact]
    public async Task Resume_UnboundSession_AnotherCaller_NotFound()
    {
        var session = TicketConversationFixtures.Session();
        session.TicketKey = null;
        _context.Add(session);
        await _context.SaveChangesAsync();

        (await Resumer().ResumeAsync("s-1", "second-person", "dashboard", "d-9", "d-9", default))
            .Should().BeOfType<SpecDialogResumeNotFound>();
        (await Resumer().ResumeAsync("s-1", "owner", "dashboard", "d-9", "d-9", default))
            .Should().BeOfType<SpecDialogResumed>("the owner still resumes their own");
    }

    private SpecDialogResumer Resumer()
    {
        var gate = new SpecDialogTurnGate(TimeProvider.System);
        return new SpecDialogResumer(new SpecDialogSessionRepository(_context), gate, new SpecDialogPendingQuestions(gate),
            TimeProvider.System, NullLogger<SpecDialogResumer>.Instance);
    }
}

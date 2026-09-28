using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.SpecDialog;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-27-481bc: the text a bound conversation was grounded on, on the read the page already
/// issues.
/// <para>
/// No new route: the per-dialog view is guarded by the same watch check every read of a
/// conversation applies, and it is what the page fetches after every message. And no tracker call
/// — the seeding path's moved-check is the only round trip on that side, and opening a pane must
/// not pay it.
/// </para>
/// </summary>
public sealed class BoundTicketInTheViewTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;

    public BoundTicketInTheViewTests()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.EnsureCreated();
    }

    [Fact]
    public async Task BoundTicketText_ABoundConversation_AnswersTheStoredTextWithoutCallingTheTracker()
    {
        var store = new SpecDialogTicketTextRepository(_context);
        await store.SaveAsync(new SpecDialogTicketText
        {
            SessionId = "s-1",
            TicketId = "DPG-1239",
            Title = "Cannot log in",
            Text = "Title: Cannot log in\n\nthe reset link expires too early",
            Truncated = true,
            Fingerprint = "f1",
            ReadAt = new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero),
        }, CancellationToken.None);

        var held = await store.GetAsync("s-1", CancellationToken.None);

        held.Should().NotBeNull();
        held!.Text.Should().Contain("the reset link expires too early");
        held.Truncated.Should().BeTrue("the pane says so where the text is shown");
        held.ReadAt.Should().Be(new DateTimeOffset(2026, 9, 27, 9, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public async Task BoundTicketText_AConversationBoundToNoTicket_HasNoRowAndShowsNothing()
    {
        var held = await new SpecDialogTicketTextRepository(_context)
            .GetAsync("s-unbound", CancellationToken.None);

        held.Should().BeNull();
    }

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

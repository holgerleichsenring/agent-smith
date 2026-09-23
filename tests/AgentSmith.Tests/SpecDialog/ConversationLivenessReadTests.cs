using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-22-2d11a: the liveness of MANY design conversations in ONE query — what a
/// sandbox reaper needs for a whole scan, over the real durable store (SQLite
/// in-memory with the shipped migrations). A read per labelled container would put a
/// design conversation's worth of database work on a thirty-second timer.
/// </summary>
public sealed class ConversationLivenessReadTests : IDisposable
{
    private readonly SqliteConnection _connection;
    private readonly AgentSmithDbContext _context;
    private readonly SpecDialogSessionRepository _repository;

    public ConversationLivenessReadTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        _context = new AgentSmithDbContext(
            new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
        _context.Database.Migrate();
        _repository = new SpecDialogSessionRepository(_context);
    }

    [Fact]
    public async Task ListBySessionIds_ReadsTheNamedRowsAndNothingElse()
    {
        await AddAsync("d-1", isOpen: true);
        await AddAsync("d-2", isOpen: false);
        await AddAsync("d-3", isOpen: true);

        var rows = await _repository.ListBySessionIdsAsync(["d-1", "d-2"], CancellationToken.None);

        rows.Select(r => r.SessionId).Should().BeEquivalentTo(["d-1", "d-2"]);
        rows.Single(r => r.SessionId == "d-2").IsOpen.Should().BeFalse();
        rows.Single(r => r.SessionId == "d-1").Project.Should().Be("project-a");
    }

    [Fact]
    public async Task ListBySessionIds_NoIds_ReadsNothing()
    {
        await AddAsync("d-1", isOpen: true);

        (await _repository.ListBySessionIdsAsync([], CancellationToken.None)).Should().BeEmpty();
    }

    [Fact]
    public async Task DbConversationLivenessReader_ReportsOpennessAndLastActivity()
    {
        var when = DateTimeOffset.Parse("2026-09-22T11:59:00Z");
        await AddAsync("d-1", isOpen: true, lastActivityAt: when);
        var provider = new ServiceCollection()
            .AddScoped(_ => _repository)
            .BuildServiceProvider();

        var rows = await new DbConversationLivenessReader(
                provider.GetRequiredService<IServiceScopeFactory>())
            .ReadAsync(["d-1"], CancellationToken.None);

        var row = rows.Should().ContainSingle().Which;
        row.ConversationId.Should().Be("d-1");
        row.Project.Should().Be("project-a");
        row.IsOpen.Should().BeTrue();
        row.LastActivityAt.Should().Be(when);
    }

    [Fact]
    public async Task NoConversationLivenessReader_ReportsNothing_WhichIsTodaysBehaviour()
    {
        var rows = await new NoConversationLivenessReader()
            .ReadAsync(["d-1", "d-2"], CancellationToken.None);

        rows.Should().BeEmpty("a composition with no session store holds no conversation");
    }

    private Task AddAsync(string sessionId, bool isOpen, DateTimeOffset? lastActivityAt = null) =>
        _repository.AddAsync(
            new SpecDialogSession
            {
                SessionId = sessionId,
                Platform = "dashboard",
                ChannelId = "c",
                ThreadId = "t-" + sessionId,
                UserId = "person-a",
                Project = "project-a",
                IsOpen = isOpen,
                LastActivityAt = lastActivityAt ?? DateTimeOffset.UtcNow,
            },
            CancellationToken.None);

    public void Dispose()
    {
        _context.Dispose();
        _connection.Dispose();
    }
}

using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>
/// p0315a: spec-dialog sessions live in the relational system-of-record. The
/// shipped migration creates the table, a transcript written by one unit of
/// work is visible to a fresh one (survives process restart AND a Redis flush
/// — the volatile-Redis incident class), and the SessionId resume handle is
/// unique at the database level.
/// </summary>
public sealed class SpecDialogSessionPersistenceTests : IDisposable
{
    private readonly SqliteConnection _connection;

    public SpecDialogSessionPersistenceTests()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using var ctx = NewContext();
        ctx.Database.Migrate();
    }

    [Fact]
    public void Migration_CreatesSpecDialogSessionsTable()
    {
        using var ctx = NewContext();

        var act = () => ctx.SpecDialogSessions.Any();

        act.Should().NotThrow("the AddSpecDialogSessions migration must apply cleanly on SQLite");
        ctx.SpecDialogSessions.Should().BeEmpty();
    }

    [Fact]
    public async Task Session_TranscriptRoundTrips_AcrossUnitsOfWork()
    {
        using (var write = NewContext())
        {
            write.SpecDialogSessions.Add(NewSession("s-1", "th-1",
                transcriptJson: """[{"role":"user","text":"first thought","at":"2026-07-08T10:00:00+00:00"}]"""));
            await write.SaveChangesAsync();
        }

        using var read = NewContext();
        var loaded = read.SpecDialogSessions.Single(s => s.SessionId == "s-1");

        loaded.TranscriptJson.Should().Contain("first thought",
            "the transcript is durable — a Redis flush cannot lose it");
        loaded.IsOpen.Should().BeTrue();
        loaded.CreatedAt.Should().NotBe(default, "SaveChanges must stamp the audit columns");
    }

    [Fact]
    public async Task SessionId_UniqueIndex_RejectsDuplicate()
    {
        using (var first = NewContext())
        {
            first.SpecDialogSessions.Add(NewSession("s-dup", "th-1"));
            await first.SaveChangesAsync();
        }

        using var second = NewContext();
        second.SpecDialogSessions.Add(NewSession("s-dup", "th-2"));

        var caught = await Record.ExceptionAsync(() => second.SaveChangesAsync());

        caught.Should().BeOfType<DbUpdateException>();
        new SqliteUniqueViolationTranslator().IsUniqueViolation((DbUpdateException)caught!)
            .Should().BeTrue("the SessionId resume handle must be unique");
    }

    private AgentSmithDbContext NewContext() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private static SpecDialogSession NewSession(
        string sessionId, string threadId, string transcriptJson = "[]") => new()
    {
        SessionId = sessionId,
        Platform = "slack",
        ChannelId = "C1",
        ThreadId = threadId,
        UserId = "U1",
        Project = "sample",
        ReposJson = """["repo-a"]""",
        TranscriptJson = transcriptJson,
        LastActivityAt = DateTimeOffset.UtcNow,
    };

    public void Dispose() => _connection.Dispose();
}

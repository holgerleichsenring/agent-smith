using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>2026-11-22-d434b: whether a run waits for its model place now is its open wait row, as the series reads it.</summary>
public sealed class ModelRunWaitClockTests : IDisposable
{
    private static readonly DateTimeOffset T = new(2026, 11, 22, 19, 37, 0, TimeSpan.Zero);
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task IsWaitingAsync_OpenWait_IsTrue_EndedOrNone_IsFalse()
    {
        await using (var db = Context())
        {
            db.Add(new ModelRunWait { RunId = "waiting", WaitingSince = T });
            db.Add(new ModelRunWait { RunId = "ended", WaitedMs = 60_000 });
            await db.SaveChangesAsync();
        }

        await using var read = Context();
        var clock = new ModelRunWaitClock(read);
        (await clock.IsWaitingAsync("waiting", default)).Should().BeTrue();
        (await clock.IsWaitingAsync("ended", default)).Should().BeFalse();
        (await clock.IsWaitingAsync("none", default)).Should().BeFalse();
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}

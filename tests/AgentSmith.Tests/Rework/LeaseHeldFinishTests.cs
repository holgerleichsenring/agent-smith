using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Moq;

namespace AgentSmith.Tests.Rework;

/// <summary>2026-10-08-e8b9e: a run that ended because another live run holds the ticket owes nothing —
/// its taken-ticket record belongs to the holder; a run whose own claim failed in the prologue clears it.</summary>
public sealed class LeaseHeldFinishTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly Mock<ITakenTicketStore> _taken = new();

    public void Dispose() => _connection.Dispose();

    private AgentSmithDbContext Db() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    private async Task FinishAsync(string runId, string? holder)
    {
        await using (var db = Db())
        {
            db.Add(new Run { Id = runId, Project = "p", TicketId = "42", Pipeline = "code", Status = "queued", StartedAt = DateTimeOffset.UtcNow });
            db.Add(new ActiveRun { Project = "p", TicketId = "42", RunId = holder, ClaimedAt = DateTimeOffset.UtcNow, HeartbeatAt = DateTimeOffset.UtcNow });
            await db.SaveChangesAsync();
        }
        await using var uow = Db();
        await new RunFinalizationProjection(new QueuedRunProjection(), null, _taken.Object)
            .ApplyAsync(uow, new RunFinishedEvent(runId, "failed", null, "held", DateTimeOffset.UtcNow), CancellationToken.None);
    }

    [Fact]
    public async Task Execute_AttachHeld_DoesNotClearTakenTicketOrFinalizeTicket()
    {
        await FinishAsync("run-refused", holder: "run-live");

        _taken.Verify(t => t.ClearAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Execute_PrologueFailsAfterClaim_ClearsTakenRecord()
    {
        // The claim inserted an unattached row (RunId null); the run failed before attaching to it.
        await FinishAsync("run-own", holder: null);

        _taken.Verify(t => t.ClearAsync("p", "42", It.IsAny<CancellationToken>()), Times.Once);
    }
}

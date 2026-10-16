using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-16-86e6a: the terminal event's end kind lands on the run row beside its summary.</summary>
public sealed class RunFinalizationProjectionEndKindTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunFinalizationProjection_EndKind_IsWrittenOnTheRunRow()
    {
        using (var seed = Context())
        {
            seed.Runs.Add(new Run
            {
                Id = "run-1", Project = "p", Pipeline = "code", TicketId = "42", Status = "running",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            });
            await seed.SaveChangesAsync();
        }

        using (var ctx = Context())
            await new RunFinalizationProjection(new QueuedRunProjection()).ApplyAsync(ctx,
                new RunFinishedEvent("run-1", "failed", null, "Pipeline aborted: missing bootstrap",
                    DateTimeOffset.UtcNow, 0m, RunEndKinds.BootstrapMissing), CancellationToken.None);

        using var check = Context();
        var run = await check.Runs.SingleAsync(r => r.Id == "run-1");
        run.EndKind.Should().Be(RunEndKinds.BootstrapMissing);
        run.Summary.Should().Be("Pipeline aborted: missing bootstrap");
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}

using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Runs;
using AgentSmith.Domain.Models;
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

    [Fact]
    public async Task RunFinalizationProjection_EndDetail_IsWrittenOnTheRunRowAndReadsBack()
    {
        using (var seed = Context())
        {
            seed.Runs.Add(new Run
            {
                Id = "run-2", Project = "p", Pipeline = "code", TicketId = "42", Status = "running",
                StartedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
            });
            await seed.SaveChangesAsync();
        }
        var detail = new EndDetail("Image node:22 (context default)", ["web"],
            "Docker Hub official library images", "Trust", "/config/settings/sandbox");

        using (var ctx = Context())
            await new RunFinalizationProjection(new QueuedRunProjection()).ApplyAsync(ctx,
                new RunFinishedEvent("run-2", "failed", null, "Preflight failed", DateTimeOffset.UtcNow, 0m,
                    RunEndKinds.ImageUntrusted, detail), CancellationToken.None);

        using var check = Context();
        var read = EndDetailJson.Read((await check.Runs.SingleAsync(r => r.Id == "run-2")).EndDetailJson);
        read.Should().BeEquivalentTo(detail);
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);
}

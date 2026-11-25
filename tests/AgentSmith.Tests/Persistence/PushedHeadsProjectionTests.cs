using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-11-25-098f: a run's pushed heads land on its row, each sha once, a replay included.</summary>
public sealed class PushedHeadsProjectionTests : IDisposable
{
    private static readonly DateTimeOffset T = DateTimeOffset.Parse("2026-11-25T10:00:00Z");
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();

    [Fact]
    public void With_AppendsASha_Once()
    {
        PushedHeads.With(null, "a").Should().Be("[\"a\"]");
        PushedHeads.With("[\"a\"]", "b").Should().Be("[\"a\",\"b\"]");
        PushedHeads.With("[\"a\",\"b\"]", "a").Should().Be("[\"a\",\"b\"]");
    }

    [Fact]
    public async Task BranchPushed_IsAppendedToTheRun_AReplayChangesNothing()
    {
        await using (var ctx = Context())
        {
            ctx.Add(new Run { Id = "r1", Project = "p", TicketId = "7", Pipeline = "code", Status = "running", StartedAt = T });
            await ctx.SaveChangesAsync();
        }
        var applier = RunEventAppliers.Default();
        foreach (var sha in new[] { "a", "b", "b" })
        {
            await using var uow = Context();
            await applier.ApplyAsync(uow, new BranchPushedEvent("r1", T, "agent-smith/7", sha), CancellationToken.None);
        }

        await using var read = Context();
        (await read.Set<Run>().SingleAsync()).PushedHeadsJson.Should().Be("[\"a\",\"b\"]");
    }

    private AgentSmithDbContext Context() => new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options);

    public void Dispose() => _connection.Dispose();
}

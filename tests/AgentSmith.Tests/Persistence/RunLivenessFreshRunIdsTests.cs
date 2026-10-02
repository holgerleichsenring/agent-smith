using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace AgentSmith.Tests.Persistence;

/// <summary>2026-10-02-75dc: the fresh run ids the sandbox reapers union into their live set.</summary>
public sealed class RunLivenessFreshRunIdsTests : IDisposable
{
    private static readonly TimeSpan FreshFor = TimeSpan.FromMinutes(3);
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunLiveness_FreshRunIds_ExcludesFinishedAndStaleRuns()
    {
        Seed("beating", started: _now.AddMinutes(-30), beat: _now.AddMinutes(-1));
        Seed("just-started", started: _now.AddSeconds(-20), beat: null);
        Seed("silent", started: _now.AddMinutes(-30), beat: _now.AddMinutes(-4));
        Seed("finished", started: _now.AddMinutes(-5), beat: _now.AddMinutes(-1), finished: _now);

        using var ctx = new AgentSmithDbContext(Options());
        var ids = await new RunLivenessRepository(ctx).GetFreshRunIdsAsync(FreshFor, _now, CancellationToken.None);

        ids.Should().BeEquivalentTo("beating", "just-started");
    }

    private void Seed(string id, DateTimeOffset started, DateTimeOffset? beat, DateTimeOffset? finished = null)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project",
            Status = finished is null ? "running" : "success",
            StartedAt = started, HeartbeatAt = beat, FinishedAt = finished,
        });
        ctx.SaveChanges();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}

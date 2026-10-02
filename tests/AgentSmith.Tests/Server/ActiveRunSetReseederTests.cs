using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5ab2c: the housekeeping leader rebuilds the Redis active-run set from the rows —
/// the fresh, unfinished, unparked ones — and never re-adds a run Redis already saw finish.
/// </summary>
public sealed class ActiveRunSetReseederTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly FakeRedisStreams _redis = new();
    private readonly ServiceProvider _provider;
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public ActiveRunSetReseederTests()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunLivenessRepository>();
        _provider = services.BuildServiceProvider();
    }

    public void Dispose()
    {
        _provider.Dispose();
        _connection.Dispose();
    }

    [Fact]
    public async Task ActiveRunSetReseeder_EmptySet_AddsFreshRunningRuns()
    {
        Seed("beating", "running", started: _now.AddMinutes(-30), beat: _now.AddMinutes(-1));
        Seed("just-started", "running", started: _now.AddSeconds(-20), beat: null);

        var added = await Reseeder().ReseedAsync(CancellationToken.None);

        added.Should().Be(2);
        ActiveSet().Should().BeEquivalentTo("beating", "just-started");
    }

    [Fact]
    public async Task ActiveRunSetReseeder_RunInRecentList_IsNotAdded()
    {
        // RunFinished reached Redis; the broadcaster has not projected it, so the row still runs.
        Seed("just-finished", "running", started: _now.AddMinutes(-5), beat: _now.AddSeconds(-10));
        _redis.State.ListLeftPush(EventStreamKeys.RecentRunsList, "just-finished");

        var added = await Reseeder().ReseedAsync(CancellationToken.None);

        added.Should().Be(0);
        ActiveSet().Should().BeEmpty();
    }

    [Fact]
    public async Task ActiveRunSetReseeder_QueuedRowWithFreshBeat_IsAdded()
    {
        // The flush lost the RunStarted that would have promoted the row; its driver beats on.
        Seed("promotion-lost", "queued", started: _now.AddMinutes(-20), beat: _now.AddSeconds(-30));

        await Reseeder().ReseedAsync(CancellationToken.None);

        ActiveSet().Should().BeEquivalentTo("promotion-lost");
    }

    [Fact]
    public async Task ActiveRunSetReseeder_StaleOrParkedRun_IsNotAdded()
    {
        Seed("silent", "running", started: _now.AddMinutes(-30), beat: _now.AddMinutes(-4));
        Seed("parked", "waiting_for_input", started: _now.AddMinutes(-1), beat: _now.AddSeconds(-5));
        Seed("finished", "success", started: _now.AddMinutes(-2), beat: _now.AddSeconds(-5), finished: _now);

        var added = await Reseeder().ReseedAsync(CancellationToken.None);

        added.Should().Be(0);
        ActiveSet().Should().BeEmpty();
    }

    private ActiveRunSetReseeder Reseeder() => new(
        _redis.Connection,
        new DbRunHeartbeat(_provider.GetRequiredService<IServiceScopeFactory>(), TimeProvider.System),
        NullLogger<ActiveRunSetReseeder>.Instance);

    private IEnumerable<string> ActiveSet() =>
        _redis.State.SetMembers(EventStreamKeys.ActiveRunsSet).Select(m => m.ToString());

    private void Seed(string id, string status, DateTimeOffset started, DateTimeOffset? beat,
        DateTimeOffset? finished = null)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project", Status = status,
            StartedAt = started, HeartbeatAt = beat, FinishedAt = finished,
        });
        ctx.SaveChanges();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;
}

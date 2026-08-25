using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Services.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrailRow = AgentSmith.Infrastructure.Persistence.Entities.RunEvent;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-24-ca23: one run's real path through a real drain — the fake Redis stream seam, the
/// real publisher, the real broadcaster and a real SQLite store. A "server process" is a fresh
/// provider over the same database file, so a restart is expressible.
/// <para>
/// Two rhythms matter and both are the production one. A run must be DISCOVERED before it
/// pauses (the publisher clears a paused run from the active set, and the drain polls at
/// 200ms), and a RELAUNCH must stay discoverable for at least one poll — that is the moment
/// the drain would mint a fresh position and replay the run's history. Publishing a whole leg
/// inside one tick hides the very defect these tests exist for.
/// </para>
/// </summary>
public sealed class WaitingRunHarness : IDisposable
{
    public const string RunId = "2026-08-24T19-46-27-ca23";
    public static readonly TimeSpan CiSafeWait = TimeSpan.FromSeconds(60);

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"ca23-waiting-{Guid.NewGuid():N}.db");
    private readonly FakeRedisStreams _redis = new();
    private readonly RedisEventPublisher _publisher;
    private readonly List<ServiceProvider> _providers = new();

    public WaitingRunHarness()
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        _publisher = new RedisEventPublisher(
            _redis.Connection, new EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
    }

    public FakeRedisState Redis => _redis.State;

    public void Dispose()
    {
        foreach (var provider in _providers) provider.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    public JobsBroadcaster NewServerProcess()
    {
        var provider = BuildProvider();
        _providers.Add(provider);
        var router = new RunEventRouter(
            Mock.Of<IRunEventFanout>(), new SandboxExpansionRegistry(),
            new SandboxDetailEventClassifier(), new SandboxActivityCoalescer(),
            new RunDbEventPersistence(provider.GetRequiredService<RunDbProjector>()));
        return new JobsBroadcaster(
            _redis.Connection, Mock.Of<IRunEventFanout>(), router,
            NullLogger<JobsBroadcaster>.Instance, new EventEnvelopeSerializer(),
            provider.GetRequiredService<IRunTerminalReconciler>(),
            provider.GetRequiredService<IUnfinishedRunSource>());
    }

    public Task PublishStartAsync() => _publisher.PublishAsync(new RunStartedEvent(
        RunId, "ticket", "add-feature", new[] { "repo" }, DateTimeOffset.UtcNow, "claude", "42"));

    /// <summary>Start a run and prove the drain discovered it before anything else happens.</summary>
    public async Task<bool> StartAndAwaitDiscoveryAsync()
    {
        await PublishStartAsync();
        return await WaitUntilAsync(ctx => ctx.Runs.Any(r => r.Id == RunId), CiSafeWait);
    }

    /// <summary>Relaunch, and stay in the active set long enough for the drain to poll.</summary>
    public async Task RelaunchAsync()
    {
        await PublishStartAsync();
        await Task.Delay(600);
    }

    public async Task PublishGatesAsync(int count)
    {
        for (var i = 0; i < count; i++)
            await _publisher.PublishAsync(
                new GateCheckedEvent(RunId, $"gate-{i}", true, "ok", DateTimeOffset.UtcNow));
    }

    public Task PublishParkAsync() => _publisher.PublishAsync(new RunFinishedEvent(
        RunId, "waiting_for_input", null,
        "Waiting for an operator answer — checkpointed; compute released.", DateTimeOffset.UtcNow));

    public Task<bool> AwaitTrailRowsAsync(int count) =>
        WaitUntilAsync(ctx => CountTrailRows(ctx) == count, CiSafeWait);

    public int TrailRows()
    {
        using var ctx = new AgentSmithDbContext(Options());
        return CountTrailRows(ctx);
    }

    public bool RunIsFinished()
    {
        using var ctx = new AgentSmithDbContext(Options());
        return ctx.Runs.Single(r => r.Id == RunId).FinishedAt is not null;
    }

    public List<long> TrailSequences()
    {
        using var ctx = new AgentSmithDbContext(Options());
        return ctx.Set<TrailRow>().Where(e => e.RunId == RunId).Select(e => e.Seq).ToList();
    }

    private static int CountTrailRows(AgentSmithDbContext ctx) =>
        ctx.Set<TrailRow>().Count(e => e.RunId == RunId);

    private ServiceProvider BuildProvider()
    {
        // The production registration, not a hand-copied list of it — a projection added
        // there and missed here would make this harness quietly unlike the server it stands in for.
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RunDbProjector>();
        services.AddSingleton<IRunTerminalReconciler, RunTerminalReconciler>();
        return services.BuildServiceProvider();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;

    private async Task<bool> WaitUntilAsync(Func<AgentSmithDbContext, bool> condition, TimeSpan timeout)
    {
        var deadline = DateTimeOffset.UtcNow + timeout;
        while (DateTimeOffset.UtcNow < deadline)
        {
            using (var ctx = new AgentSmithDbContext(Options()))
                if (condition(ctx)) return true;
            await Task.Delay(50);
        }
        return false;
    }
}

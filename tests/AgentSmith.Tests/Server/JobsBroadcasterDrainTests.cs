using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using Microsoft.AspNetCore.SignalR;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrailRow = AgentSmith.Infrastructure.Persistence.Entities.RunEvent;

namespace AgentSmith.Tests.Server;

/// <summary>
/// p0378: the RunFinished drain persistence contract, proven over the fake
/// Redis stream seam + a real SQLite store. Live evidence (run fb2d): a
/// high-volume green run's RunFinishedEvent sat as the newest stream entry
/// while the DB row stayed Status='running' — the terminal event must reach
/// the DB exactly once, including across a broadcaster restart that lands
/// while the drain cursor still lags the stream tail.
/// </summary>
public sealed class JobsBroadcasterDrainTests : IDisposable
{
    private const string RunId = "2026-07-24T10-00-00-fb2d";

    private readonly string _dbPath = Path.Combine(
        Path.GetTempPath(), $"p0378-drain-{Guid.NewGuid():N}.db");
    private readonly FakeRedisStreams _redis = new();
    private readonly RedisEventPublisher _publisher;
    private readonly List<ServiceProvider> _providers = new();

    public JobsBroadcasterDrainTests()
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Database.Migrate();
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        _publisher = new RedisEventPublisher(
            _redis.Connection, new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
    }

    public void Dispose()
    {
        foreach (var provider in _providers) provider.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task Broadcaster_HighVolumeRun_RunFinishedPersists_StatusTerminal()
    {
        // Arrange: process A drains the run's early events, then goes down while
        // the drain cursor still lags a stream that gains the terminal event.
        var processA = NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        await PublishStartAsync();
        await PublishGatesAsync(120);
        (await WaitForRowAsync())
            .Should().BeTrue("the early drain must create the run row");
        await processA.StopAsync(CancellationToken.None);
        await PublishGatesAsync(150); // more than one count:100 drain cycle
        await PublishFinishAsync();   // terminal event lands while no drain is up

        // Act: process B cold-starts over the same Redis + DB.
        var processB = NewServerProcess();
        await processB.StartAsync(CancellationToken.None);
        var persisted = await WaitForTerminalRowAsync();
        await processB.StopAsync(CancellationToken.None);

        // Assert
        persisted.Should().BeTrue(
            "the terminal RunFinished sits in the stream and must reach the DB");
        using var check = new AgentSmithDbContext(Options());
        var run = check.Runs.Single(r => r.Id == RunId);
        run.Status.Should().Be("success");
        run.FinishedAt.Should().NotBeNull();
        CountRunFinishedTrailRows(check).Should().Be(1);

    }

    [Fact]
    public async Task Broadcaster_DrainLagsThenQuiesces_CursorReachesRunFinished()
    {
        // Arrange: one continuously running process; the stream outruns the
        // count:100-per-cycle drain by several cycles before quiescing.
        var process = NewServerProcess();
        await process.StartAsync(CancellationToken.None);
        await PublishStartAsync();
        (await WaitForRowAsync())
            .Should().BeTrue("the run must be discovered and tracked first");
        await PublishGatesAsync(250);
        await PublishFinishAsync();

        // Act. p0475: both waits happen while the broadcaster is still up. StopAsync cancels
        // the drain loop's token, so anything not yet written is abandoned — by design, since
        // the stream is the source of truth and a restart re-reads it. Waiting for the trail
        // row AFTER the stop asked the only producer to do work it had been told to abandon,
        // which passed on a fast machine and timed out on a loaded CI runner.
        var persisted = await WaitForTerminalRowAsync();
        var trailed = await WaitForTrailRowAsync();
        await process.StopAsync(CancellationToken.None);

        // Assert
        persisted.Should().BeTrue("a lagging cursor must catch up to the quiesced stream tail");
        using var check = new AgentSmithDbContext(Options());
        check.Runs.Single(r => r.Id == RunId).Status.Should().Be("success");
        trailed.Should().BeTrue("the terminal event reaches the trail, not only the run row");
        CountRunFinishedTrailRows(check).Should().Be(1);
    }

    [Fact]
    public async Task Broadcaster_RunFinished_ProcessedExactlyOnce_NoDuplicateTrailRow()
    {
        // Arrange: a fully drained + persisted run …
        var processA = NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        await PublishStartAsync();
        (await WaitForRowAsync())
            .Should().BeTrue("the run must be discovered and tracked first");
        await PublishGatesAsync(5);
        await PublishFinishAsync();
        var fullyPersisted = await WaitUntilAsync(
            ctx => ctx.Runs.Any(r => r.Id == RunId && r.FinishedAt != null)
                   && CountRunFinishedTrailRows(ctx) == 1);
        fullyPersisted.Should().BeTrue("the live drain persists the terminal event + trail row");
        await processA.StopAsync(CancellationToken.None);

        // Act: … then a restart rehydrates the terminal run from the stream.
        var processB = NewServerProcess();
        await processB.StartAsync(CancellationToken.None);
        // Several drain passes in which a wrong re-processing would show, counted from the
        // drain's own discoveries. 2026-09-22-3f7c: half a second was several passes on the
        // machine that wrote it and part of one on the runner that failed this class.
        (await AwaitDrainPassesAsync(processB, 3)).Should().BeTrue();
        await processB.StopAsync(CancellationToken.None);

        // Assert: rehydrate must not re-persist the already-recorded terminal event.
        using var check = new AgentSmithDbContext(Options());
        CountRunFinishedTrailRows(check).Should().Be(1);
        check.Runs.Single(r => r.Id == RunId).Status.Should().Be("success");
    }

    private JobsBroadcaster NewServerProcess()
    {
        var provider = BuildProvider();
        _providers.Add(provider);
        var router = new RunEventRouter(
            Mock.Of<IRunEventFanout>(), new SandboxExpansionRegistry(),
            new SandboxDetailEventClassifier(), new SandboxActivityCoalescer(),
            new RunDbEventPersistence(provider.GetRequiredService<RunDbProjector>()),
            new FiledWorkNudge(Mock.Of<IHubContext<JobsHub>>(), new FiledWorkWatchRegistry()));
        return new JobsBroadcaster(
            _redis.Connection, Mock.Of<IRunEventFanout>(), router,
            NullLogger<JobsBroadcaster>.Instance,
            new AgentSmith.Infrastructure.Services.Events.EventEnvelopeSerializer(),
            provider.GetRequiredService<IRunTerminalReconciler>());
    }

    // One "server process": a fresh provider whose scoped IUnitOfWork opens a
    // fresh context over the SAME database file — restart = new provider.
    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<AgentSmith.Infrastructure.Persistence.Services.RunTrailBuffers>();
        services.AddSingleton<RunDbProjector>();
        services.AddSingleton<IRunTerminalReconciler, RunTerminalReconciler>();
        return services.BuildServiceProvider();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>()
            .UseSqlite($"Data Source={_dbPath}").Options;

    private Task PublishStartAsync() => _publisher.PublishAsync(new RunStartedEvent(
        RunId, "ticket", "fix-bug", new[] { "repo" }, DateTimeOffset.UtcNow, "claude", "42"));

    private async Task PublishGatesAsync(int count)
    {
        for (var i = 0; i < count; i++)
            await _publisher.PublishAsync(
                new GateCheckedEvent(RunId, $"gate-{i}", true, "ok", DateTimeOffset.UtcNow));
    }

    private Task PublishFinishAsync() => _publisher.PublishAsync(new RunFinishedEvent(
        RunId, "success", null, "done", DateTimeOffset.UtcNow, 0.5m));

    private Task<bool> WaitForRowAsync() =>
        WaitUntilAsync(ctx => ctx.Runs.Any(r => r.Id == RunId));

    private Task<bool> WaitForTerminalRowAsync() =>
        WaitUntilAsync(ctx => ctx.Runs.Any(r => r.Id == RunId && r.FinishedAt != null));

    /// <summary>
    /// One drain pass discovers whatever is in the active set, so a run published here and
    /// then seen in <see cref="JobsBroadcaster.Active"/> proves a pass happened. A negative
    /// claim is worth the number of passes it watched, and only counting says what that was.
    /// </summary>
    private async Task<bool> AwaitDrainPassesAsync(JobsBroadcaster drain, int passes)
    {
        for (var pass = 0; pass < passes; pass++)
        {
            var tracer = $"{RunId}-pass-{pass}";
            await _publisher.PublishAsync(new RunStartedEvent(
                tracer, "ticket", "fix-bug", new[] { "repo" }, DateTimeOffset.UtcNow, "claude", "42"));
            if (!await TestWaits.ReachedAsync(() => drain.Active.ContainsKey(tracer))) return false;
        }
        return true;
    }

    /// <summary>
    /// p0443: the run row and its trail row are two writes, so waiting for the first and
    /// asserting the second is a race the assertion loses under load. CI hit it on the
    /// release build: persisted true, status success, trail rows 0 — the projector had
    /// landed the run and not yet the trail. Wait for what is actually asserted.
    /// </summary>
    private Task<bool> WaitForTrailRowAsync() =>
        WaitUntilAsync(ctx => CountRunFinishedTrailRows(ctx) == 1);

    private Task<bool> WaitUntilAsync(Func<AgentSmithDbContext, bool> condition) =>
        TestWaits.ReachedAsync(() =>
        {
            using var ctx = new AgentSmithDbContext(Options());
            return condition(ctx);
        });

    private static int CountRunFinishedTrailRows(AgentSmithDbContext ctx) =>
        ctx.Set<TrailRow>().Count(e => e.RunId == RunId && e.Type == nameof(EventType.RunFinished));
}

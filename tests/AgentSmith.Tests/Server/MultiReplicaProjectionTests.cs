using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Extensions;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Server.Hubs;
using AgentSmith.Server.Services.Events;
using AgentSmith.Server.Services.SpecDialog;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TrailRow = AgentSmith.Infrastructure.Persistence.Entities.RunEvent;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-26-0f19a: every replica drains every run stream and projects it into the one store,
/// so the position an event is recorded under must be the same on every replica and across a
/// restart. Each "replica" here is a real <see cref="JobsBroadcaster"/> with its own projector
/// stack, over one fake Redis and one SQLite file.
/// </summary>
public sealed class MultiReplicaProjectionTests : IDisposable
{
    private const string RunId = "2026-10-26T09-00-00-0f19";

    private readonly string _dbPath = Path.Combine(Path.GetTempPath(), $"0f19a-{Guid.NewGuid():N}.db");
    private readonly FakeRedisStreams _redis = new();
    private readonly RedisEventPublisher _publisher;
    private readonly List<ServiceProvider> _providers = new();
    private readonly List<JobsBroadcaster> _replicas = new();

    public MultiReplicaProjectionTests()
    {
        using var ctx = new AgentSmithDbContext(Options());
        MigratedStoreTemplate.CopyInto(ctx);
        ctx.Database.ExecuteSqlRaw("PRAGMA journal_mode=WAL;");
        _publisher = new RedisEventPublisher(
            _redis.Connection, new EventEnvelopeSerializer(), NullLogger<RedisEventPublisher>.Instance);
    }

    public void Dispose()
    {
        foreach (var replica in _replicas) replica.StopAsync(CancellationToken.None).GetAwaiter().GetResult();
        foreach (var provider in _providers) provider.Dispose();
        SqliteConnection.ClearAllPools();
        File.Delete(_dbPath);
    }

    [Fact]
    public async Task SecondReplica_StartsMidRun_EveryCallRecordedOnce()
    {
        var first = await StartReplicaAsync();
        await PublishStartAsync();
        await PublishCallsAsync(3);
        (await WaitUntilAsync(ctx => ctx.RunLlmCalls.Count(c => c.RunId == RunId) == 3)).Should().BeTrue();

        var second = await StartReplicaAsync();
        await PublishCallsAsync(1);
        await PublishFinishAsync();
        (await BothDrainedAsync(first, second)).Should().BeTrue();

        using var check = new AgentSmithDbContext(Options());
        check.RunLlmCalls.Count(c => c.RunId == RunId).Should().Be(4, "four calls were made, whichever replica recorded them");
        check.Runs.Single(r => r.Id == RunId).CostTotalUsd.Should().Be(4m);
        TrailTypes(check).Should().Equal(
            nameof(EventType.RunStarted), nameof(EventType.LlmCallFinished), nameof(EventType.LlmCallFinished),
            nameof(EventType.LlmCallFinished), nameof(EventType.LlmCallFinished), nameof(EventType.RunFinished));
    }

    [Fact]
    public async Task Restart_BeforeTrailFlush_LaterCallsStillRecorded()
    {
        var crashed = await StartReplicaAsync();
        await PublishStartAsync();
        await PublishCallsAsync(2);
        (await WaitUntilAsync(ctx => ctx.RunLlmCalls.Count(c => c.RunId == RunId) == 2)).Should().BeTrue();
        await crashed.StopAsync(CancellationToken.None); // the trail buffer dies unflushed

        var restarted = await StartReplicaAsync();
        await PublishCallsAsync(2);
        await PublishFinishAsync();
        (await BothDrainedAsync(restarted, restarted)).Should().BeTrue();

        using var check = new AgentSmithDbContext(Options());
        check.RunLlmCalls.Count(c => c.RunId == RunId).Should().Be(4, "no call made after the restart may be dropped");
        check.Runs.Single(r => r.Id == RunId).CostTotalUsd.Should().Be(4m);
    }

    /// <summary>
    /// A replica that lags projects an event after another replica already has. The sandbox
    /// result's fold carries no record of its own, so it is the case a per-row check cannot see.
    /// </summary>
    [Fact]
    public async Task LaggingReplica_ProjectsASandboxResultAgain_FoldsItOnce()
    {
        var events = new RunEvent[]
        {
            new RunStartedEvent(RunId, "ticket", "code", ["repo"], DateTimeOffset.UtcNow, "claude", "42"),
            new SandboxResultEvent(RunId, "repo", "dotnet build", 0, 1_000, DateTimeOffset.UtcNow),
        };
        foreach (var projector in new[] { NewProjector(), NewProjector() })
            for (var i = 0; i < events.Length; i++)
                await projector.ProjectAsync(events[i], StreamTrailPosition.Of($"1760000000000-{i}"), CancellationToken.None);

        using var check = new AgentSmithDbContext(Options());
        AgentSmith.Contracts.Runs.RunStoryJson.TryDeserialize<AgentSmith.Contracts.Runs.RunMetrics>(
                check.Runs.Single(r => r.Id == RunId).RunMetricsJson)!
            .SandboxCommandMs.Should().Be(1_000, "one command ran, however many replicas projected it");
    }

    private RunDbProjector NewProjector()
    {
        var provider = BuildProvider();
        _providers.Add(provider);
        return provider.GetRequiredService<RunDbProjector>();
    }

    private async Task<JobsBroadcaster> StartReplicaAsync()
    {
        var provider = BuildProvider();
        _providers.Add(provider);
        var router = new RunEventRouter(
            Mock.Of<IRunEventFanout>(), new SandboxExpansionRegistry(),
            new SandboxDetailEventClassifier(), new SandboxActivityCoalescer(),
            new RunDbEventPersistence(provider.GetRequiredService<RunDbProjector>()),
            new FiledWorkNudge(Mock.Of<IHubContext<JobsHub>>(), new FiledWorkWatchRegistry()));
        var replica = new JobsBroadcaster(
            _redis.Connection, Mock.Of<IRunEventFanout>(), router,
            NullLogger<JobsBroadcaster>.Instance, new EventEnvelopeSerializer(),
            provider.GetRequiredService<IRunTerminalReconciler>());
        _replicas.Add(replica);
        await replica.StartAsync(CancellationToken.None);
        return replica;
    }

    private ServiceProvider BuildProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddRunProjections();
        services.AddSingleton<RunEventApplier>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<RunTrailBuffers>();
        services.AddSingleton<RunDbProjector>();
        services.AddSingleton<IRunTerminalReconciler, RunTerminalReconciler>();
        return services.BuildServiceProvider();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={_dbPath}").Options;

    private Task PublishStartAsync() => _publisher.PublishAsync(new RunStartedEvent(
        RunId, "ticket", "code", new[] { "repo" }, DateTimeOffset.UtcNow, "claude", "42"));

    private async Task PublishCallsAsync(int count)
    {
        for (var i = 0; i < count; i++)
            await _publisher.PublishAsync(new LlmCallFinishedEvent(
                RunId, "claude", "master", 10, 5, 1m, 100, DateTimeOffset.UtcNow));
    }

    private Task PublishFinishAsync() => _publisher.PublishAsync(new RunFinishedEvent(
        RunId, "success", null, "done", DateTimeOffset.UtcNow, null));

    // A replica removes a run from Active only after it has persisted the run's ending.
    private Task<bool> BothDrainedAsync(JobsBroadcaster one, JobsBroadcaster other) =>
        TestWaits.ReachedAsync(() => !one.Active.ContainsKey(RunId) && !other.Active.ContainsKey(RunId)
            && one.Recent.Any(r => r.RunId == RunId) && other.Recent.Any(r => r.RunId == RunId));

    private Task<bool> WaitUntilAsync(Func<AgentSmithDbContext, bool> condition) =>
        TestWaits.ReachedAsync(() =>
        {
            using var ctx = new AgentSmithDbContext(Options());
            return condition(ctx);
        });

    private static List<string> TrailTypes(AgentSmithDbContext ctx) =>
        [.. ctx.Set<TrailRow>().Where(e => e.RunId == RunId).OrderBy(e => e.Seq).Select(e => e.Type)];
}

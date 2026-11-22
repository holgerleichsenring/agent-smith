using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Infrastructure.Persistence.Services.Translators;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>
/// 2026-11-20-a52f: a free place goes to the live run that waited longest on its key, whichever replica it waits on —
/// two stores with their own signals over one database stand for two replicas.
/// </summary>
public sealed class ModelRunWaitQueueTests : IDisposable
{
    private const string Key = "azure_openai|https://shared.openai.azure.com|gpt41";
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"agentsmith-queue-{Guid.NewGuid():N}.db");
    private readonly ModelSlotsClock _clock = new();

    public ModelRunWaitQueueTests() => MigratedStoreTemplate.CopyToFile(_path);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        File.Delete(_path);
    }

    [Fact]
    public async Task Acquire_ThreeWaitersOnTwoReplicas_TakeThePlaceInTheOrderTheyStartedWaiting()
    {
        var (one, two) = (Store(), Store());
        await HeldByAsync(one, "run-holder");
        await WaitAsync(one, "run-a");
        await WaitAsync(two, "run-b");
        await WaitAsync(one, "run-c");

        await one.ReleaseAsync("run-holder", CancellationToken.None);
        (await AskAsync(one, "run-c")).Should().BeFalse("run-a and run-b waited longer");
        (await AskAsync(two, "run-b")).Should().BeFalse("run-a waited longer");
        (await AskAsync(one, "run-a")).Should().BeTrue();
        await one.ReleaseAsync("run-a", CancellationToken.None);
        (await AskAsync(one, "run-c")).Should().BeFalse("run-b on the other replica is next");
        (await AskAsync(two, "run-b")).Should().BeTrue();
        await two.ReleaseAsync("run-b", CancellationToken.None);
        (await AskAsync(one, "run-c")).Should().BeTrue();
    }

    [Fact]
    public async Task Acquire_RunWithoutAWait_NeverPassesARecordedWaiter_EvenAfterItsOwnProcessReleased()
    {
        var (one, two) = (Store(), Store());
        await HeldByAsync(one, "run-holder");
        await WaitAsync(two, "run-a");
        await SeedRunAsync("run-new");

        await one.ReleaseAsync("run-holder", CancellationToken.None);

        (await AskAsync(one, "run-new")).Should().BeFalse("run-a waits on the other replica and came first");
        (await AskAsync(two, "run-a")).Should().BeTrue();
    }

    [Fact]
    public async Task Acquire_WaiterWhoseRunEnded_IsSkipped()
    {
        var store = Store();
        await WaitAsync(store, "run-dead", finished: true);
        await WaitAsync(store, "run-b");

        (await AskAsync(store, "run-b")).Should().BeTrue("an ended run's open wait does not stall the queue");
    }

    [Fact]
    public async Task Acquire_WaiterThatStoppedBeating_IsSkipped_AFreshRowlessWaiterStillCounts()
    {
        var store = Store();
        await WaitAsync(store, "run-silent", heartbeatAt: _clock.GetUtcNow() - TimeSpan.FromMinutes(10), startedAt: _clock.GetUtcNow() - TimeSpan.FromHours(1));
        await WaitAsync(store, "run-rowless", seedRow: false);
        await WaitAsync(store, "run-b");

        (await AskAsync(store, "run-b")).Should().BeFalse("a waiter whose start event has not been projected yet keeps its turn");
        (await AskAsync(store, "run-rowless")).Should().BeTrue("the silent waiter is past the liveness bound");
    }

    [Fact]
    public async Task Acquire_ResumedWaiter_CountsFromItsNewStart_NotItsBeatBeforeThePark()
    {
        var store = Store();
        await WaitAsync(store, "run-resumed", heartbeatAt: _clock.GetUtcNow() - TimeSpan.FromHours(2)); // StartedAt is now
        await WaitAsync(store, "run-b");

        (await AskAsync(store, "run-b")).Should().BeFalse("the resumed run is alive by its new start, as the liveness reaper reads it");
    }

    [Fact]
    public async Task WaitStarted_OnAnotherKey_DoesNotCarryAnOldWaitIntoThisQueue()
    {
        var store = Store();
        await SeedRunAsync("run-moved");
        await store.WaitStartedAsync("run-moved", "other-key", CancellationToken.None); // its end of wait was lost
        _clock.Advance(TimeSpan.FromMinutes(1));
        await WaitAsync(store, "run-a");
        await store.WaitStartedAsync("run-moved", Key, CancellationToken.None);

        (await AskAsync(store, "run-moved")).Should().BeFalse("run-a started waiting on this key first");
        (await AskAsync(store, "run-a")).Should().BeTrue();
    }

    [Fact]
    public async Task Acquire_TwoFreePlaces_AdmitTheTwoOldestWaiters()
    {
        var store = Store();
        await WaitAsync(store, "run-a");
        await WaitAsync(store, "run-b");
        await WaitAsync(store, "run-c");

        (await AskAsync(store, "run-c", limit: 2)).Should().BeFalse();
        (await AskAsync(store, "run-b", limit: 2)).Should().BeTrue("one waiter is ahead of it and two places are free");
        (await AskAsync(store, "run-a", limit: 2)).Should().BeTrue();
        (await AskAsync(store, "run-c", limit: 2)).Should().BeFalse("both places are taken");
    }

    [Fact]
    public async Task Acquire_LoweredLimit_AdmitsNobodyWhileThePlacesAreFull()
    {
        var store = Store();
        await HeldByAsync(store, "run-holder", limit: 2);
        await HeldByAsync(store, "run-other", limit: 2);
        await SeedRunAsync("run-a");

        (await AskAsync(store, "run-a", limit: 1)).Should().BeFalse();
    }

    [Fact]
    public void WaiterFreshFor_IsTheLivenessBound() =>
        ModelRunWaitQueue.WaiterFreshFor.Should().Be(ActiveRunReaper.LeaseFreshFor);

    private async Task HeldByAsync(DbModelRunSlots store, string runId, int limit = 1)
    {
        await SeedRunAsync(runId);
        (await AskAsync(store, runId, limit)).Should().BeTrue();
    }

    // A run refused while the place is held records its wait, as the gate does; the clock moves so the order is plain.
    private async Task WaitAsync(DbModelRunSlots store, string runId, bool finished = false, DateTimeOffset? heartbeatAt = null,
        bool seedRow = true, DateTimeOffset? startedAt = null)
    {
        if (seedRow) await SeedRunAsync(runId, finished, heartbeatAt, startedAt);
        await store.WaitStartedAsync(runId, Key, CancellationToken.None);
        _clock.Advance(TimeSpan.FromSeconds(1));
    }

    private static Task<bool> AskAsync(DbModelRunSlots store, string runId, int limit = 1) =>
        store.AcquireAsync(Key, limit, runId, CancellationToken.None);

    private async Task SeedRunAsync(string runId, bool finished = false, DateTimeOffset? heartbeatAt = null, DateTimeOffset? startedAt = null)
    {
        await using var db = new AgentSmithDbContext(Options());
        db.Runs.Add(new Run
        {
            Id = runId, Project = "p", Pipeline = "code", TicketId = runId, Status = finished ? "failed" : "running",
            StartedAt = startedAt ?? _clock.GetUtcNow(), FinishedAt = finished ? _clock.GetUtcNow() : null,
            HeartbeatAt = heartbeatAt ?? _clock.GetUtcNow(),
        });
        await db.SaveChangesAsync();
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={_path}").Options;

    private DbModelRunSlots Store()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddSingleton<IUniqueViolationTranslator>(new SqliteUniqueViolationTranslator());
        return new DbModelRunSlots(services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            new ModelRunSlotSignal(), _clock);
    }
}

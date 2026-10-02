using System.Runtime.CompilerServices;
using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Claim;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-02-5ab2b: a request is claimed at the pop, before the semaphore wait, and its row is
/// beaten from then on — so a claimed request waiting for a slot never reads as dead. Real rows
/// in a store on disk, because the beat writes from its own thread while the test reads.
/// </summary>
public sealed class PipelineQueueConsumerClaimTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"agentsmith-claim-{Guid.NewGuid():N}.db");
    private readonly TaskCompletionSource<bool> _firstMayStart = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public PipelineQueueConsumerClaimTests() => MigratedStoreTemplate.CopyToFile(_path);

    [Fact]
    public async Task PipelineQueueConsumer_ClaimedRequestWaitingOnSemaphore_BeatsItsRow()
    {
        Seed("run-a", request: null);
        Seed("run-b", request: "{}");
        var consumer = NewConsumer(Request("run-a"), Request("run-b"));

        var running = consumer.RunAsync(CancellationToken.None);
        await TestWaits.UntilAsync(() => Row("run-b").ClaimedAt is not null, "run-b to be claimed at the pop");
        var claimedAt = Row("run-b").ClaimedAt!.Value;
        await TestWaits.UntilAsync(() => Row("run-b").HeartbeatAt > claimedAt, "run-b's row to be beaten");

        running.IsCompleted.Should().BeFalse("run-a still holds the only slot, so run-b waits on the semaphore");
        _firstMayStart.SetResult(true);
        await running;
    }

    private PipelineQueueConsumer NewConsumer(params PipelineRequest[] requests)
    {
        var scopes = Scopes();
        var cancelState = new Mock<IRunCancelStateReader>();
        cancelState.Setup(c => c.IsStartRefusedAsync("run-a", It.IsAny<CancellationToken>())).Returns(_firstMayStart.Task);
        var gate = TestRunStartGate.Over(cancelState.Object,
            new DbRunStartClaim(scopes, TimeProvider.System), new DbRunHeartbeat(scopes, TimeProvider.System),
            new HurriedClock());
        var services = new ServiceCollection();
        services.AddSingleton(Mock.Of<IEventPublisher>());
        services.AddSingleton<IActiveRunLease>(new NoOpActiveRunLease());
        return new PipelineQueueConsumer(
            services.BuildServiceProvider(), new ListQueue(requests), gate, "config.yaml",
            maxParallelJobs: 1, shutdownGraceSeconds: 5, NullLogger<PipelineQueueConsumer>.Instance);
    }

    private IServiceScopeFactory Scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => Context());
        services.AddScoped<QueuedRunRepository>().AddScoped<RunLivenessRepository>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private static PipelineRequest Request(string runId) =>
        new("p1", "init-project", IsInit: true, Headless: true, RunId: runId);

    private void Seed(string id, string? request)
    {
        using var db = Context();
        db.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project", Status = "queued", StartedAt = DateTimeOffset.UtcNow,
            QueuedRequestJson = request, RequestEnqueuedAt = request is null ? null : DateTimeOffset.UtcNow,
        });
        db.SaveChanges();
    }

    private Run Row(string id)
    {
        using var db = Context();
        return db.Runs.AsNoTracking().Single(r => r.Id == id);
    }

    private AgentSmithDbContext Context() =>
        new(new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite($"Data Source={_path}").Options);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        foreach (var file in new[] { _path, _path + "-wal", _path + "-shm" })
            if (File.Exists(file)) File.Delete(file);
    }

    // The heartbeat pump's 45-second wait, shortened to 20 ms of real time.
    private sealed class HurriedClock : TimeProvider
    {
        private static readonly TimeSpan Hurried = TimeSpan.FromMilliseconds(20);

        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
            System.CreateTimer(callback, state, Hurried, period == Timeout.InfiniteTimeSpan ? period : Hurried);
    }

    private sealed class ListQueue(IReadOnlyList<PipelineRequest> requests) : IRedisJobQueue
    {
        public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        public async IAsyncEnumerable<PipelineRequest> ConsumeAsync([EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var request in requests) yield return request;
            await Task.CompletedTask;
        }

        public Task<long> LenAsync(CancellationToken cancellationToken) => Task.FromResult((long)requests.Count);
    }
}

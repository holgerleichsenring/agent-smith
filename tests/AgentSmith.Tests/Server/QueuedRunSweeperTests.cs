using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using RunEvent = AgentSmith.Contracts.Events.RunEvent;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5ab2b: what Redis lost of a stored request is recovered from its row — an unclaimed
/// request is pushed again with its context, a claim whose consumer stopped beating ends
/// interrupted. Real rows over a migrated SQLite store.
/// </summary>
public sealed class QueuedRunSweeperTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly List<PipelineRequest> _pushed = [];
    private readonly List<RunEvent> _published = [];
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task QueuedRunSweeper_UnclaimedRowOlderThan60s_IsPushedAgainWithItsContext()
    {
        Seed("lost", enqueued: _now.AddMinutes(-2));
        Seed("young", enqueued: _now.AddSeconds(-10));

        await NewSweeper().RunOnceAsync(CancellationToken.None);

        var pushed = _pushed.Should().ContainSingle().Subject;
        pushed.RunId.Should().Be("lost");
        ((JsonElement)pushed.Context![ContextKeys.AutoCompletePullRequests]).GetBoolean().Should().BeTrue();
        Row("lost").RequestEnqueuedAt.Should().BeAfter(_now.AddSeconds(-1), "the push time is renewed");
        Row("lost").CancelRequested.Should().BeFalse();
    }

    [Fact]
    public async Task QueuedRunSweeper_ClaimedRowWithoutBeatFor3Minutes_IsFlaggedInterrupted()
    {
        Seed("dead", enqueued: _now.AddMinutes(-10), claimed: _now.AddMinutes(-9), beat: _now.AddMinutes(-4));
        Seed("waiting", enqueued: _now.AddMinutes(-10), claimed: _now.AddMinutes(-9), beat: _now.AddSeconds(-30));

        await NewSweeper().RunOnceAsync(CancellationToken.None);

        Row("dead").CancelReason.Should().Be(RunLivenessReaper.InterruptedReason);
        Row("waiting").CancelRequested.Should().BeFalse("its consumer still beats it on the semaphore");
        _published.OfType<RunCancelRequestedEvent>().Should().ContainSingle().Which.RunId.Should().Be("dead");
        _pushed.Should().BeEmpty("a claimed request is never pushed again");
    }

    [Fact]
    public async Task QueuedRunSweeper_FlaggedOrFinishedRow_IsLeftAlone()
    {
        Seed("cancelled", enqueued: _now.AddMinutes(-5), flagged: true);

        await NewSweeper().RunOnceAsync(CancellationToken.None);

        _pushed.Should().BeEmpty();
    }

    [Fact]
    public async Task QueuedRunSweeper_SuspendGap_SuppressesVerdicts()
    {
        var clock = new ReadCountingClock();
        using var cts = new CancellationTokenSource();
        var loop = NewSweeper(clock).RunAsync(TimeSpan.FromMilliseconds(10), cts.Token);
        await WaitForReadsAsync(clock, 6);

        clock.Advance(TimeSpan.FromMinutes(30)); // the host slept
        await WaitForReadsAsync(clock, 6);
        Seed("dead", enqueued: _now.AddMinutes(-10), claimed: _now.AddMinutes(-9), beat: _now.AddMinutes(-5));
        await WaitForReadsAsync(clock, 12);
        Row("dead").CancelRequested.Should().BeFalse("every beat is stale after a suspend");

        for (var i = 0; i < 4; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            await WaitForReadsAsync(clock, 6);
        }
        await TestWaits.UntilAsync(() => Row("dead").CancelRequested, "the sweeper to resume verdicts");
        cts.Cancel();
        await loop;
        Row("dead").CancelReason.Should().Be(RunLivenessReaper.InterruptedReason);
    }

    private QueuedRunSweeper NewSweeper(TimeProvider? clock = null)
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => MigratedStoreTemplate.Context(_connection));
        services.AddScoped<QueuedRunRepository>().AddScoped<RunRepository>();
        var queue = new Mock<IRedisJobQueue>();
        queue.Setup(q => q.EnqueueAsync(It.IsAny<PipelineRequest>(), It.IsAny<CancellationToken>()))
            .Callback<PipelineRequest, CancellationToken>((r, _) => _pushed.Add(r)).Returns(Task.CompletedTask);
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => _published.Add(ev)).Returns(Task.CompletedTask);
        return new QueuedRunSweeper(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(), queue.Object, events.Object,
            clock ?? TimeProvider.System, NullLogger<QueuedRunSweeper>.Instance);
    }

    private void Seed(
        string id, DateTimeOffset enqueued, DateTimeOffset? claimed = null, DateTimeOffset? beat = null, bool flagged = false)
    {
        var request = new PipelineRequest("p1", "init-project", IsInit: true, Headless: true, RunId: id,
            Context: new Dictionary<string, object> { [ContextKeys.AutoCompletePullRequests] = true });
        using var db = MigratedStoreTemplate.Context(_connection);
        db.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project", Status = "queued", StartedAt = enqueued,
            QueuedRequestJson = JsonSerializer.Serialize(request), RequestEnqueuedAt = enqueued,
            ClaimedAt = claimed, HeartbeatAt = beat, CancelRequested = flagged,
        });
        db.SaveChanges();
    }

    private Run Row(string id)
    {
        using var db = MigratedStoreTemplate.Context(_connection);
        return db.Runs.AsNoTracking().Single(r => r.Id == id);
    }

    private static Task WaitForReadsAsync(ReadCountingClock clock, long delta)
    {
        var target = clock.Reads + delta;
        return TestWaits.UntilAsync(() => clock.Reads >= target, "the sweeper loop to progress");
    }
}

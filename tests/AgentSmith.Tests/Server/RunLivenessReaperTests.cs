using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence;
using AgentSmith.Infrastructure.Persistence.Contracts;
using AgentSmith.Infrastructure.Persistence.Entities;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Infrastructure.Persistence.Services;
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
/// 2026-10-02-5f89e: a run no process drives any more ends as interrupted. Real rows over a
/// migrated SQLite store; the verdict is the flag + event the wall-time backstop writes, so
/// CancelEnforcer ends it exactly as it ends a wall-time run.
/// </summary>
public sealed class RunLivenessReaperTests : IDisposable
{
    private readonly SqliteConnection _connection = MigratedStoreTemplate.OpenCopy();
    private readonly List<RunEvent> _published = [];
    private readonly RunCancellationRegistry _registry = new(NullLogger<RunCancellationRegistry>.Instance);
    private readonly DateTimeOffset _now = DateTimeOffset.UtcNow;

    public void Dispose() => _connection.Dispose();

    [Fact]
    public async Task RunLivenessReaper_StaleRunningRow_FlagsInterruptedAndPublishesCancelRequested()
    {
        Seed("run-1", "running", started: _now.AddHours(-1), beat: _now.AddMinutes(-5));

        var flagged = await NewReaper().RunOnceAsync(CancellationToken.None);

        flagged.Should().Be(1);
        var run = Row("run-1");
        run.CancelRequested.Should().BeTrue();
        run.CancelReason.Should().Be("interrupted");
        run.CancelDeadlineAt.Should().NotBeNull().And.BeOnOrBefore(DateTimeOffset.UtcNow,
            "nothing is left to cancel cooperatively — the enforcer ends it on its next scan");
        _published.OfType<RunCancelRequestedEvent>().Should().ContainSingle()
            .Which.Should().Match<RunCancelRequestedEvent>(e => e.RunId == "run-1" && e.Reason == "interrupted");
    }

    [Fact]
    public async Task RunLivenessReaper_NoHeartbeatYet_UsesStartedAt()
    {
        Seed("old", "running", started: _now.AddMinutes(-4), beat: null);
        Seed("young", "running", started: _now.AddSeconds(-30), beat: null);

        await NewReaper().RunOnceAsync(CancellationToken.None);

        Row("old").CancelReason.Should().Be("interrupted", "StartedAt is the first beat");
        Row("young").CancelRequested.Should().BeFalse("a run younger than its first renewal is alive");
    }

    [Fact]
    public async Task RunLivenessReaper_ResumedRunWithAPreParkBeat_IsJudgedByItsRestart()
    {
        // A resumed run's StartedAt is reset at promotion while its beat dates from before
        // the park — the later of the two is its last sign of life.
        Seed("resumed", "running", started: _now.AddSeconds(-20), beat: _now.AddHours(-6));

        await NewReaper().RunOnceAsync(CancellationToken.None);

        Row("resumed").CancelRequested.Should().BeFalse();
    }

    [Fact]
    public async Task RunLivenessReaper_RunAliveInThisProcess_IsRefreshedNotReaped()
    {
        Seed("run-local", "running", started: _now.AddHours(-1), beat: _now.AddMinutes(-10));
        _registry.Register("run-local", CancellationToken.None);

        var flagged = await NewReaper().RunOnceAsync(CancellationToken.None);

        flagged.Should().Be(0);
        var run = Row("run-local");
        run.CancelRequested.Should().BeFalse();
        run.HeartbeatAt.Should().BeAfter(_now.AddMinutes(-1), "a lagging pump's beat is refreshed");
        _published.Should().BeEmpty();
    }

    [Fact]
    public async Task RunLivenessReaper_FreshHeartbeatFromAnotherReplica_IsLeftAlone()
    {
        Seed("run-b", "running", started: _now.AddHours(-2), beat: _now.AddSeconds(-40));

        var flagged = await NewReaper().RunOnceAsync(CancellationToken.None);

        flagged.Should().Be(0);
        Row("run-b").CancelRequested.Should().BeFalse();
    }

    [Fact]
    public async Task RunLivenessReaper_WaitingForInputAndQueuedRows_AreNeverCandidates()
    {
        Seed("parked", "waiting_for_input", started: _now.AddDays(-2), beat: _now.AddDays(-2));
        Seed("queued", "queued", started: _now.AddHours(-3), beat: null);

        var flagged = await NewReaper().RunOnceAsync(CancellationToken.None);

        flagged.Should().Be(0);
        Row("parked").CancelRequested.Should().BeFalse();
        Row("queued").CancelRequested.Should().BeFalse();
    }

    [Fact]
    public async Task RunLivenessReaper_SuspendGap_SuppressesVerdicts()
    {
        var clock = new ReadCountingClock();
        using var cts = new CancellationTokenSource();
        var loop = NewReaper(clock).RunAsync(TimeSpan.FromMilliseconds(10), cts.Token);
        await WaitForReadsAsync(clock, 6);

        clock.Advance(TimeSpan.FromMinutes(30)); // the host slept
        await WaitForReadsAsync(clock, 6);
        Seed("run-1", "running", started: _now.AddHours(-1), beat: _now.AddMinutes(-5));
        await WaitForReadsAsync(clock, 12);
        Row("run-1").CancelRequested.Should().BeFalse("every beat is stale after a suspend");

        // Wake-time passes in steps under the gap threshold; one large jump would read as a
        // second suspend.
        for (var i = 0; i < 4; i++)
        {
            clock.Advance(TimeSpan.FromMinutes(1));
            await WaitForReadsAsync(clock, 6);
        }
        await WaitForAsync(() => Row("run-1").CancelRequested);
        cts.Cancel();
        await loop;
        Row("run-1").CancelReason.Should().Be("interrupted", "after one grace window verdicts resume");
    }

    private RunLivenessReaper NewReaper(TimeProvider? clock = null)
    {
        var scopes = Scopes();
        var events = new Mock<IEventPublisher>();
        events.Setup(e => e.PublishAsync(It.IsAny<RunEvent>(), It.IsAny<CancellationToken>()))
            .Callback<RunEvent, CancellationToken>((ev, _) => _published.Add(ev))
            .Returns(Task.CompletedTask);
        return new RunLivenessReaper(
            scopes, _registry, new DbRunHeartbeat(scopes, TimeProvider.System), events.Object,
            clock ?? TimeProvider.System, NullLogger<RunLivenessReaper>.Instance);
    }

    private IServiceScopeFactory Scopes()
    {
        var services = new ServiceCollection();
        services.AddScoped<IUnitOfWork>(_ => new AgentSmithDbContext(Options()));
        services.AddScoped<RunRepository>().AddScoped<RunLivenessRepository>();
        return services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>();
    }

    private void Seed(string id, string status, DateTimeOffset started, DateTimeOffset? beat)
    {
        using var ctx = new AgentSmithDbContext(Options());
        ctx.Runs.Add(new Run
        {
            Id = id, Project = "p1", Pipeline = "init-project", Status = status,
            StartedAt = started, HeartbeatAt = beat,
        });
        ctx.SaveChanges();
    }

    private Run Row(string id)
    {
        using var ctx = new AgentSmithDbContext(Options());
        return ctx.Runs.AsNoTracking().Single(r => r.Id == id);
    }

    private DbContextOptions<AgentSmithDbContext> Options() =>
        new DbContextOptionsBuilder<AgentSmithDbContext>().UseSqlite(_connection).Options;

    // Progress is counted in clock reads, never wall time: an iteration reads the monotonic
    // clock at least twice, so a delta of 2N reads proves N iterations ran.
    private static Task WaitForReadsAsync(ReadCountingClock clock, long delta)
    {
        var target = clock.Reads + delta;
        return WaitForAsync(() => clock.Reads >= target);
    }

    private static Task WaitForAsync(Func<bool> condition) =>
        TestWaits.UntilAsync(condition, "the reaper loop to progress");

    private sealed class ReadCountingClock : TimeProvider
    {
        private long _timestamp;
        private long _reads;
        public long Reads => Volatile.Read(ref _reads);
        public override long GetTimestamp()
        {
            Interlocked.Increment(ref _reads);
            return Volatile.Read(ref _timestamp);
        }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan by) => Interlocked.Add(ref _timestamp, by.Ticks);
    }
}

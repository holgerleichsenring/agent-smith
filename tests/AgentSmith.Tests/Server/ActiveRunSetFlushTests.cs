using AgentSmith.Server.Services.Events;
using AgentSmith.Tests.TestHelpers;
using AgentSmith.Tests.TestSupport;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-10-02-5ab2c: a Redis flush takes the active-run set, the run's stream and its stored
/// position; the run keeps beating its row and publishing. The server that comes up after the
/// flush has nothing in Redis to find the run by — the database has to name it, or its
/// RunFinished never reaches the row. In-memory Redis fake over a real SQLite store.
/// </summary>
public sealed class ActiveRunSetFlushTests : IDisposable
{
    private readonly WaitingRunHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task FlushedSet_InMemoryRedisFake_ReseedAndBroadcasterBringRunFinishedToTheRow()
    {
        var drain = await ServerUpAfterFlushAsync(new SettableClock());
        await _harness.PublishGatesAsync(3); // the run's stream is re-created, without a RunStarted
        (await _harness.IsInActiveSetAsync()).Should().BeFalse("a flush left nothing in the set");

        var added = await _harness.NewReseeder().ReseedAsync(CancellationToken.None);

        added.Should().Be(1);
        (await _harness.IsInActiveSetAsync()).Should().BeTrue("the leader re-seeds the run from its fresh row");
        (await TestWaits.ReachedAsync(() => drain.Active.ContainsKey(WaitingRunHarness.RunId)))
            .Should().BeTrue("the broadcaster discovers the re-seeded run");
        await _harness.PublishFinishAsync("success");
        (await _harness.AwaitFinishedAsync()).Should().BeTrue("RunFinished must reach the row");
        _harness.RunStatus().Should().Be("success");
        await drain.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task JobsBroadcaster_UntrackedUnfinishedRunWithStream_IsDrainedIntoTheDatabase()
    {
        var clock = new SettableClock();
        var drain = await ServerUpAfterFlushAsync(clock);
        await _harness.PublishGatesAsync(3);

        clock.Now += JobsBroadcaster.DatabaseDiscoveryInterval;
        (await TestWaits.ReachedAsync(() => drain.Active.ContainsKey(WaitingRunHarness.RunId)))
            .Should().BeTrue("the database pass tracks the run the set no longer lists");
        (await _harness.IsInActiveSetAsync()).Should().BeFalse("no re-seed ran: the store alone named the run");
        await _harness.PublishFinishAsync("success");

        (await _harness.AwaitFinishedAsync()).Should().BeTrue("RunFinished must reach the row");
        await drain.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task JobsBroadcaster_ParkedRunWithoutCursor_IsNotTracked()
    {
        var first = _harness.NewServerProcess();
        await first.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await _harness.PublishParkAsync();
        (await TestWaits.ReachedAsync(() => _harness.RunStatus() == "waiting_for_input")).Should().BeTrue();
        await first.StopAsync(CancellationToken.None);
        _harness.FlushRedis();
        var clock = new SettableClock();
        var drain = _harness.NewServerProcess(clock: clock);
        await drain.StartAsync(CancellationToken.None);

        await _harness.PublishGatesAsync(3); // a stream for the parked run, and no stored position
        clock.Now += JobsBroadcaster.DatabaseDiscoveryInterval;
        (await _harness.AwaitDrainPassesAsync(drain, 3)).Should().BeTrue();

        drain.Active.Should().NotContainKey(WaitingRunHarness.RunId,
            "a parked run with no position would be read from 0-0; it waits for its relaunch");
        await drain.StopAsync(CancellationToken.None);
    }

    /// <summary>The run started under one server; Redis was flushed; another server came up
    /// and has made its first database pass — before the run's stream existed again.</summary>
    private async Task<JobsBroadcaster> ServerUpAfterFlushAsync(SettableClock clock)
    {
        var first = _harness.NewServerProcess();
        await first.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue("the row must exist");
        await first.StopAsync(CancellationToken.None);
        _harness.FlushRedis();
        var drain = _harness.NewServerProcess(clock: clock);
        await drain.StartAsync(CancellationToken.None);
        (await _harness.AwaitDrainPassesAsync(drain, 2)).Should().BeTrue();
        return drain;
    }
}

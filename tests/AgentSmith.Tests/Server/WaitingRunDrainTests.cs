using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-08-24-ca23: a run that stops WITHOUT ending keeps its place in its own stream. Live
/// evidence: a run parked on an operator question was relaunched onto its own id, the drain
/// re-seeded at the stream's beginning because the run held no position, and every one of its
/// 1343 events was re-projected 42 times — inflating the trail, the per-call rows and the cost
/// total, and making the dashboard replay the run's whole history on a loop.
/// </summary>
public sealed class WaitingRunDrainTests : IDisposable
{
    private readonly WaitingRunHarness _harness = new();

    public void Dispose() => _harness.Dispose();

    /// <summary>
    /// The trap this guards is the WRONG fix: the drain breaks on a terminal event BEFORE it
    /// advances, so a position merely retained would point at the entry before the pause and
    /// re-read it on every 200ms poll — the same engine at five hertz, for the whole wait.
    /// </summary>
    [Fact]
    public async Task Park_TheCursorAdvancesPastTheParkEventSoItIsNeverReprocessed()
    {
        var process = _harness.NewServerProcess();
        await process.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await _harness.PublishGatesAsync(5);
        await _harness.PublishParkAsync();
        (await _harness.AwaitTrailRowsAsync(7))
            .Should().BeTrue("start + 5 gates + the park event are seven rows");

        await Task.Delay(1500); // several drain cycles in which a stuck position would re-read
        await process.StopAsync(CancellationToken.None);

        _harness.TrailRows().Should().Be(7, "the park event must be projected exactly once");
        _harness.RunIsFinished().Should().BeFalse("a park is a pause, so the run is not finished");
    }

    [Fact]
    public async Task Park_ThenResume_NoHistoricalEventIsWrittenToTheTrailTwice()
    {
        var process = _harness.NewServerProcess();
        await process.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await _harness.PublishGatesAsync(20);
        await _harness.PublishParkAsync();
        (await _harness.AwaitTrailRowsAsync(22)).Should().BeTrue("the first leg reaches the trail");

        await _harness.RelaunchAsync();
        await _harness.PublishGatesAsync(3);
        await _harness.PublishParkAsync();
        var resumed = await _harness.AwaitTrailRowsAsync(27);
        await Task.Delay(1000);
        await process.StopAsync(CancellationToken.None);

        resumed.Should().BeTrue("the resumed leg's own events must reach the trail");
        _harness.TrailRows().Should().Be(27,
            "22 from the first leg plus 5 from the second — no event may be replayed");
    }

    [Fact]
    public async Task Restart_DuringAWait_ThenResume_TheDrainResumesAtTheStoredPositionAndReplaysNothing()
    {
        var processA = _harness.NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await _harness.PublishGatesAsync(10);
        await _harness.PublishParkAsync();
        (await _harness.AwaitTrailRowsAsync(12)).Should().BeTrue("the first leg reaches the trail");
        await processA.StopAsync(CancellationToken.None);

        // A new process holds no position in memory — only the stored one can save it.
        var processB = _harness.NewServerProcess();
        await processB.StartAsync(CancellationToken.None);
        await _harness.RelaunchAsync();
        await _harness.PublishGatesAsync(2);
        await _harness.PublishParkAsync();
        var resumed = await _harness.AwaitTrailRowsAsync(16);
        await Task.Delay(1000);
        await processB.StopAsync(CancellationToken.None);

        resumed.Should().BeTrue("the resumed leg's events must reach the trail");
        _harness.TrailRows().Should().Be(16, "a restart must not replay the first leg");
    }

    /// <summary>A missing position is a fallback, not a failure — the point is that it survives.</summary>
    [Fact]
    public async Task Restart_DuringAWait_TheStoredPositionIsGone_TheDrainStartsFreshWithoutFailing()
    {
        var processA = _harness.NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await _harness.PublishGatesAsync(4);
        await _harness.PublishParkAsync();
        (await _harness.AwaitTrailRowsAsync(6)).Should().BeTrue();
        await processA.StopAsync(CancellationToken.None);
        _harness.Redis.KeyDelete($"run:{WaitingRunHarness.RunId}:cursor"); // e.g. a Redis flush

        var processB = _harness.NewServerProcess();
        await processB.StartAsync(CancellationToken.None);
        await Task.Delay(1000);
        await processB.StopAsync(CancellationToken.None);

        _harness.RunIsFinished().Should().BeFalse();
    }

    [Fact]
    public async Task TrailSequence_AcrossManyLegs_IsStrictlyIncreasing()
    {
        var process = _harness.NewServerProcess();
        await process.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await _harness.PublishGatesAsync(6);
        await _harness.PublishParkAsync();
        (await _harness.AwaitTrailRowsAsync(8)).Should().BeTrue();
        await _harness.RelaunchAsync();
        await _harness.PublishGatesAsync(6);
        await _harness.PublishParkAsync();
        var second = await _harness.AwaitTrailRowsAsync(16);
        await process.StopAsync(CancellationToken.None);

        second.Should().BeTrue("the second leg's events reach the trail");
        _harness.TrailSequences().Should().OnlyHaveUniqueItems(
            "a leg that restarted its counter would re-mint sequences the store already holds");
    }
}

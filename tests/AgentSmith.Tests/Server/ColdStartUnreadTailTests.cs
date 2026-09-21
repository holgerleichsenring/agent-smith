using AgentSmith.Contracts.Events;
using AgentSmith.Infrastructure.Persistence.Contracts;
using FluentAssertions;

namespace AgentSmith.Tests.Server;

/// <summary>
/// 2026-09-21-de50: an operator's surface showed runs waiting for a queue they had already
/// left. Their terminal event was in the stream and neither cold-start repair reached it —
/// the recent-runs list had evicted them (fifty entries, churned by every terminal publish)
/// and the unfinished-run anchor lands ON the tail, so the drain reads strictly past the one
/// event the row has never seen. The tail is now read where the anchor is chosen, and a
/// non-waiting terminal event there is RECONCILED, never delivered.
/// </summary>
public sealed class ColdStartUnreadTailTests : IDisposable
{
    private readonly WaitingRunHarness _harness = new();
    private readonly RecordedRepairs _repairs = new();

    public void Dispose() => _harness.Dispose();

    [Fact]
    public async Task ColdStart_AnUnfinishedRunWhoseTailIsItsTerminalEvent_FinishesTheRow()
    {
        await ARunLeftUnfinishedWithAnUnreadTailAsync("success");

        _harness.RunIsFinished().Should().BeTrue(
            "the stream still holds the terminal event no process ever persisted");
        _harness.RunStatus().Should().Be("success");
        _repairs.Statuses().Should().Equal("success");
    }

    [Fact]
    public async Task ColdStart_TheRepairedRun_HasExactlyOneTerminalTrailRow()
    {
        await ARunLeftUnfinishedWithAnUnreadTailAsync("success");

        _harness.TerminalTrailRows().Should().Be(1,
            "the reconciler appends the trail row only when none exists");
    }

    /// <summary>
    /// The refused alternative anchored one entry EARLIER so the live drain would deliver the
    /// event. Nothing about where the cursor lands changed, and the proof is that the three
    /// gate events published while no process was up are still never replayed.
    /// </summary>
    [Fact]
    public async Task ColdStart_TheCursor_IsStillAnchoredAtTheTail()
    {
        await ARunLeftUnfinishedWithAnUnreadTailAsync("success");

        _harness.TrailRows().Should().Be(WaitingRunHarness.TrailFlushThreshold + 1,
            "the first process's own trail plus the reconciled terminal row — the three gates "
            + "behind the anchor are history the cold start must not replay");
    }

    [Fact]
    public async Task ColdStart_AnUnfinishedRunWhoseTailIsNotTerminal_IsLeftAlone()
    {
        var processA = _harness.NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await AwaitTheFirstProcessTrailAsync();
        await processA.StopAsync(CancellationToken.None);
        _harness.DropActivePointer(); // the index is gone; the stream is not

        await ColdStartAsync();

        _repairs.Statuses().Should().BeEmpty("a gate event is not a terminal event");
        _harness.RunIsFinished().Should().BeFalse();
        _harness.TrailRows().Should().Be(WaitingRunHarness.TrailFlushThreshold,
            "nothing behind the anchor may be replayed");
    }

    [Fact]
    public async Task ColdStart_AWaitingTerminalTail_LeavesTheRunUnfinished()
    {
        await ARunLeftUnfinishedWithAnUnreadTailAsync(null); // a park

        _repairs.Statuses().Should().BeEmpty(
            "a park is a pause, and the cold start asks that question itself");
        _harness.RunIsFinished().Should().BeFalse("a parked run stays on the active surface");
    }

    [Fact]
    public async Task ColdStart_AQueuedTerminalTail_CreatesNoQueueEntry()
    {
        await ARunLeftUnfinishedWithAnUnreadTailAsync("queued", project: "sample");

        _harness.QueueEntries().Should().Be(0,
            "reconciling cannot re-create the capacity-queue entry a delivery would");
        _repairs.Statuses().Should().BeEmpty("'queued' is a waiting status");
        _harness.RunIsFinished().Should().BeFalse();
    }

    /// <summary>
    /// The recent-runs walk runs FIRST, and the unfinished set is read after it — so a run it
    /// has already mended is no longer unfinished by the time the anchor looks.
    /// </summary>
    [Fact]
    public async Task ColdStart_ARunAlreadyRepairedFromTheRecentList_IsUnchangedBySecondReconciliation()
    {
        var processA = _harness.NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync()).Should().BeTrue();
        await AwaitTheFirstProcessTrailAsync();
        await processA.StopAsync(CancellationToken.None);
        await _harness.PublishFinishAsync("success"); // stays on the recent-runs list

        await ColdStartAsync();

        _repairs.Statuses().Should().Equal(["success"],
            "the recent-list walk repairs it, and the anchor no longer calls it unfinished");
        _harness.RunIsFinished().Should().BeTrue();
        _harness.TerminalTrailRows().Should().Be(1);
        _harness.TrailRows().Should().Be(WaitingRunHarness.TrailFlushThreshold + 1);
    }

    /// <summary>
    /// One process discovers the run and dies; the run's remaining events — ending in its
    /// terminal one, or in a park when <paramref name="status"/> is null — land with nobody
    /// draining, and the recent-runs ring evicts the row before the next process boots.
    /// </summary>
    private async Task ARunLeftUnfinishedWithAnUnreadTailAsync(string? status, string? project = null)
    {
        var processA = _harness.NewServerProcess();
        await processA.StartAsync(CancellationToken.None);
        (await _harness.StartAndAwaitDiscoveryAsync(project)).Should().BeTrue();
        await AwaitTheFirstProcessTrailAsync();
        await processA.StopAsync(CancellationToken.None);

        await _harness.PublishGatesAsync(3);
        if (status is null) await _harness.PublishParkAsync();
        else await _harness.PublishFinishAsync(status);
        _harness.EvictFromRecentList();

        await ColdStartAsync();
    }

    /// <summary>
    /// The trail is written in batches and this harness runs no background flusher, so the
    /// first process only leaves rows behind once it has drained a full batch. Without them
    /// there would be no history for the anchor to be proven not to replay.
    /// </summary>
    private async Task AwaitTheFirstProcessTrailAsync()
    {
        await _harness.PublishGatesAsync(WaitingRunHarness.TrailFlushThreshold - 1);
        (await _harness.AwaitTrailRowsAsync(WaitingRunHarness.TrailFlushThreshold))
            .Should().BeTrue("the first process writes its own trail before it dies");
    }

    // Several drain cycles inside the window, so a wrongly-placed anchor has time to replay.
    private async Task ColdStartAsync()
    {
        var processB = _harness.NewServerProcess(_repairs.Wrapping);
        await processB.StartAsync(CancellationToken.None);
        await Task.Delay(1000);
        await processB.StopAsync(CancellationToken.None);
    }

    /// <summary>Watches what the cold start hands the reconciler, and still lets it repair.</summary>
    private sealed class RecordedRepairs
    {
        private readonly List<RunFinishedEvent> _calls = new();

        public IRunTerminalReconciler Wrapping(IRunTerminalReconciler inner) => new Spy(_calls, inner);

        public string[] Statuses()
        {
            lock (_calls) return _calls.Select(c => c.Status).ToArray();
        }

        private sealed class Spy(List<RunFinishedEvent> calls, IRunTerminalReconciler inner)
            : IRunTerminalReconciler
        {
            public Task ReconcileAsync(RunFinishedEvent terminal, CancellationToken cancellationToken)
            {
                lock (calls) calls.Add(terminal);
                return inner.ReconcileAsync(terminal, cancellationToken);
            }
        }
    }
}

using AgentSmith.Application.Services.Lifecycle;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;

namespace AgentSmith.Tests.Services.Lifecycle;

/// <summary>
/// p0383: the reaper's false-positive guards. A stale DB heartbeat alone is NOT
/// proof the owning replica is gone: the run may be alive in this very process
/// (heartbeat pump behind), or the host may just have woken from suspend (every
/// heartbeat stale by construction). Genuine dead-replica reaping stays unchanged.
/// </summary>
public sealed class ActiveRunReaperTests
{
    private static readonly TicketId Ticket = new("T-1");
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMilliseconds(10);

    private readonly Mock<IActiveRunLease> _lease = new();
    private readonly Mock<IRunCancellationRegistry> _registry = new();
    private readonly Mock<IEventPublisher> _events = new();
    private readonly MonotonicFakeTimeProvider _clock = new();

    private ActiveRunReaper NewReaper() => new(
        _lease.Object, _registry.Object, _events.Object, _clock, NullLogger<ActiveRunReaper>.Instance);

    private readonly ScanCounter _scans = new();

    private void SetupStaleCandidate(string? runId) =>
        _lease.Setup(l => l.FindStaleAsync(It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Callback(_scans.Increment)
            .ReturnsAsync(new[] { new StaleLease("proj", Ticket, runId, JobId: null) });

    [Fact]
    public async Task RunOnce_StaleLease_RunAliveInProcess_RenewsHeartbeat_DoesNotReap()
    {
        SetupStaleCandidate("run-1");
        _registry.Setup(r => r.IsLocallyActive("run-1")).Returns(true);

        var released = await NewReaper().RunOnceAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        released.Should().Be(0, "a run alive in this process is never a dead replica");
        _lease.Verify(l => l.RenewHeartbeatAsync("proj", Ticket, It.IsAny<CancellationToken>()), Times.Once);
        _lease.Verify(l => l.ReleaseAsync(
            It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Never);
        _registry.Verify(r => r.TryCancel(It.IsAny<string>(), It.IsAny<string>()), Times.Never);
        _events.Verify(e => e.PublishAsync(
            It.IsAny<RunCancelRequestedEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunOnce_StaleLease_RunNotLocal_ReapsAndReleases_Unchanged()
    {
        SetupStaleCandidate("run-1");
        _registry.Setup(r => r.IsLocallyActive("run-1")).Returns(false);

        var released = await NewReaper().RunOnceAsync(TimeSpan.FromMinutes(3), CancellationToken.None);

        released.Should().Be(1, "a stale lease without local liveness is a dead replica — reap it");
        _registry.Verify(r => r.TryCancel("run-1", "stale-lease-reaped"), Times.Once);
        _events.Verify(e => e.PublishAsync(
            It.Is<RunCancelRequestedEvent>(ev => ev.RunId == "run-1"), It.IsAny<CancellationToken>()), Times.Once);
        _lease.Verify(l => l.ReleaseAsync("proj", Ticket, "run-1", It.IsAny<CancellationToken>()), Times.Once,
            "p0459: the reap releases under the run the stale lease names");
        _lease.Verify(l => l.RenewHeartbeatAsync(
            It.IsAny<string>(), It.IsAny<TicketId>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunAsync_MonotonicGapExceedsThreshold_SuppressesVerdictsForGraceWindow()
    {
        SetupStaleCandidate(runId: null);
        using var cts = new CancellationTokenSource();
        var loop = NewReaper().RunAsync(ActiveRunReaper.LeaseFreshFor, ScanInterval, cts.Token);
        await WaitForAsync(() => _scans.Count >= 2, () => _clock.Reads);

        _clock.Advance(TimeSpan.FromMinutes(6)); // the host slept past the stale threshold
        await WaitForIterationsAsync(2); // drain any iteration already past gap detection
        var scansWhenSuppressed = _scans.Count;
        await WaitForIterationsAsync(10); // many iterations later — fake clock unmoved, grace holds

        _scans.Count.Should().Be(scansWhenSuppressed, "stale verdicts are suppressed for the grace window");
        cts.Cancel();
        await loop;
    }

    [Fact]
    public async Task RunAsync_AfterGraceWindow_ReapsAgain()
    {
        SetupStaleCandidate("run-1");
        _registry.Setup(r => r.IsLocallyActive("run-1")).Returns(false);
        using var cts = new CancellationTokenSource();
        var loop = NewReaper().RunAsync(ActiveRunReaper.LeaseFreshFor, ScanInterval, cts.Token);
        await WaitForAsync(() => _scans.Count >= 1, () => _clock.Reads);
        _clock.Advance(TimeSpan.FromMinutes(6)); // suspend detected on the next iteration
        await WaitForIterationsAsync(2);
        var scansWhenSuppressed = _scans.Count;

        // Wake-time passes in sub-threshold steps (real time never jumps after the
        // wake itself); one large jump would read as a second suspend.
        for (var i = 0; i < 10 && _scans.Count == scansWhenSuppressed; i++)
        {
            _clock.Advance(TimeSpan.FromSeconds(45));
            await WaitForIterationsAsync(2);
        }

        await WaitForAsync(() => _scans.Count > scansWhenSuppressed, () => _clock.Reads);
        _lease.Verify(l => l.ReleaseAsync("proj", Ticket, "run-1", It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "after one LeaseFreshFor window of grace the reaper resumes normal reaping");
        cts.Cancel();
        await loop;
    }

    // p0423b: the budget is a HANG-DETECTOR, not a claim about speed. Progress is
    // measured in loop iterations (see WaitForIterationsAsync) precisely so this test
    // does not assert wall-clock timing — but the poll itself still needs to be
    // scheduled, and on a two-core CI runner a burst of migration-applying test classes
    // starves async continuations for seconds at a time. What is asserted is that the loop
    // progresses at all rather than deadlocking.
    // 2026-08-30-f590: a fixed budget cannot tell the two failures apart. A DEADLOCKED loop
    // makes no progress at all; a STARVED one makes progress slowly, and a runner sharing its
    // cores with two other test projects starves this loop for seconds at a stretch — four CI
    // failures across three branches in one day, every one of them here, none of them a defect.
    // So the budget is spent on STALL rather than on total time: it resets whenever the loop
    // moves. p0432's intent survives — a loop that has genuinely stopped still fails in seconds
    // — while a slow host merely takes longer.
    private static async Task WaitForAsync(Func<bool> condition, Func<long> progress)
    {
        var stallLimit = TimeSpan.FromSeconds(30);
        var lastMoved = DateTime.UtcNow;
        var seen = progress();
        while (!condition())
        {
            await Task.Delay(5);
            var now = progress();
            if (now != seen) { seen = now; lastMoved = DateTime.UtcNow; }
            else if (DateTime.UtcNow - lastMoved > stallLimit) break;
        }
        condition().Should().BeTrue(
            "the reaper loop should have progressed; it made no progress at all for {0}s, "
            + "which is a stalled loop and not a slow host", stallLimit.TotalSeconds);
    }

    // Loop progress measured by the loop itself, not by wall clock: a reaper
    // iteration reads the monotonic clock at most three times (gap detection,
    // previous-iteration stamp, grace check), so a delta of 3N clock reads proves
    // at least N full iterations ran regardless of how loaded the test host is.
    private Task WaitForIterationsAsync(int iterations) =>
        WaitForAsync(ReadsAhead(_clock, iterations * 3), () => _clock.Reads);

    private static Func<bool> ReadsAhead(MonotonicFakeTimeProvider clock, long delta)
    {
        var baseline = clock.Reads;
        return () => clock.Reads >= baseline + delta;
    }

    private sealed class ScanCounter
    {
        private int _count;
        public int Count => Volatile.Read(ref _count);
        public void Increment() => Interlocked.Increment(ref _count);
    }

    // p0383: monotonic-clock fake — GetTimestamp/GetElapsedTime are the surfaces
    // under test (suspend-gap detection); ticks advance only when the test says so.
    private sealed class MonotonicFakeTimeProvider : TimeProvider
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

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
        await AwaitAsync(_scans.ReachedAsync(2));

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
        await AwaitAsync(_scans.ReachedAsync(1));
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

        await AwaitAsync(_scans.ReachedAsync(scansWhenSuppressed + 1));
        _lease.Verify(l => l.ReleaseAsync("proj", Ticket, "run-1", It.IsAny<CancellationToken>()), Times.AtLeastOnce,
            "after one LeaseFreshFor window of grace the reaper resumes normal reaping");
        cts.Cancel();
        await loop;
    }

    // 2026-08-28-479f: the loop ANNOUNCES its progress and the test awaits that
    // announcement. It used to poll every 5 ms until a 30 s deadline — a claim about
    // scheduling dressed as a claim about progress, and a starvation source of its own:
    // three tests each spinning a timer every 5 ms for up to 30 s, inside a suite that
    // already runs three test processes at once. p0423b and p0432 swung the deadline to
    // 120 s and back to 30 s; neither direction fixes a poll that competes with the loop
    // it is waiting for.
    //
    // The ceiling that remains is a HANG-DETECTOR and nothing else: a reaper that never
    // progresses must fail rather than hang the suite forever.
    private static readonly TimeSpan HangCeiling = TimeSpan.FromSeconds(30);

    private static Task AwaitAsync(Task reached) => reached.WaitAsync(HangCeiling);

    // Loop progress measured by the loop itself, not by wall clock: a reaper
    // iteration reads the monotonic clock at most three times (gap detection,
    // previous-iteration stamp, grace check), so a delta of 3N clock reads proves
    // at least N full iterations ran regardless of how loaded the test host is.
    private Task WaitForIterationsAsync(int iterations) =>
        AwaitAsync(_clock.ReadsReachedAsync(_clock.Reads + iterations * 3));

    /// <summary>A monotonically rising count that hands out a task per milestone.</summary>
    private sealed class Milestones
    {
        private readonly object _sync = new();
        private readonly List<(long Target, TaskCompletionSource Reached)> _waiting = [];
        private long _count;

        public long Count { get { lock (_sync) return _count; } }

        public void Increment()
        {
            List<TaskCompletionSource>? reached = null;
            lock (_sync)
            {
                _count++;
                for (var i = _waiting.Count - 1; i >= 0; i--)
                {
                    if (_waiting[i].Target > _count) continue;
                    (reached ??= []).Add(_waiting[i].Reached);
                    _waiting.RemoveAt(i);
                }
            }
            if (reached is null) return;
            foreach (var waiter in reached) waiter.TrySetResult();
        }

        public Task ReachedAsync(long target)
        {
            lock (_sync)
            {
                if (_count >= target) return Task.CompletedTask;
                var waiter = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _waiting.Add((target, waiter));
                return waiter.Task;
            }
        }
    }

    private sealed class ScanCounter
    {
        private readonly Milestones _scans = new();
        public long Count => _scans.Count;
        public void Increment() => _scans.Increment();
        public Task ReachedAsync(long target) => _scans.ReachedAsync(target);
    }

    // p0383: monotonic-clock fake — GetTimestamp/GetElapsedTime are the surfaces
    // under test (suspend-gap detection); ticks advance only when the test says so.
    private sealed class MonotonicFakeTimeProvider : TimeProvider
    {
        private readonly Milestones _reads = new();
        private long _timestamp;
        public long Reads => _reads.Count;
        public Task ReadsReachedAsync(long target) => _reads.ReachedAsync(target);
        public override long GetTimestamp()
        {
            _reads.Increment();
            return Volatile.Read(ref _timestamp);
        }
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;
        public void Advance(TimeSpan by) => Interlocked.Add(ref _timestamp, by.Ticks);
    }
}

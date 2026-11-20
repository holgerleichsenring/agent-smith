using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;

namespace AgentSmith.Tests.ModelSlots;

/// <summary>2026-11-20-24bd: the store of a host without a database keeps the same contract as the database store.</summary>
public sealed class InProcessModelRunSlotsTests
{
    [Fact]
    public async Task Acquire_UpToTheLimit_ReEntrant_ThenFull_UntilARelease()
    {
        var signal = new ModelRunSlotSignal();
        var slots = new InProcessModelRunSlots(signal);

        (await slots.AcquireAsync("k", 2, "a", CancellationToken.None)).Should().BeTrue();
        (await slots.AcquireAsync("k", 2, "a", CancellationToken.None)).Should().BeTrue("re-entrant");
        (await slots.AcquireAsync("k", 2, "b", CancellationToken.None)).Should().BeTrue();
        (await slots.AcquireAsync("k", 2, "c", CancellationToken.None)).Should().BeFalse();
        (await slots.AcquireAsync("other", 2, "d", CancellationToken.None)).Should().BeTrue("another key counts apart");
        (await slots.AcquireAsync("other", 2, "a", CancellationToken.None)).Should().BeTrue("one place per run, on any key");

        var woken = signal.NextRelease;
        await slots.ReleaseAsync("a", CancellationToken.None);

        woken.IsCompleted.Should().BeTrue();
        (await slots.AcquireAsync("k", 2, "c", CancellationToken.None)).Should().BeTrue();
        signal.Entering("a").Should().NotBeNull();
    }

    [Fact]
    public async Task Forgotten_KeepsTheRunsDoor_WhileReleasedDropsIt()
    {
        var signal = new ModelRunSlotSignal();
        var door = signal.Entering("a");
        signal.Held("a", "k", DateTimeOffset.UtcNow);

        signal.Forgotten("a");

        signal.HeldBy("a").Should().BeNull();
        signal.Entering("a").Should().BeSameAs(door, "a sweep or delete can come while the run's calls use the door");
    }

    [Fact]
    public async Task Acquire_ClosesAnOpenWait_AndReleaseForgetsIt()
    {
        var clock = new ModelSlotsClock();
        var slots = new InProcessModelRunSlots(new ModelRunSlotSignal(), clock);
        await slots.WaitStartedAsync("a", CancellationToken.None);
        clock.Advance(TimeSpan.FromMinutes(10));

        await slots.AcquireAsync("k", 1, "a", CancellationToken.None); // the end of the wait was never recorded
        clock.Advance(TimeSpan.FromHours(1));

        (await slots.WaitedAsync(["a"], CancellationToken.None))["a"].Should().Be(TimeSpan.FromMinutes(10));
        await slots.ReleaseAsync("a", CancellationToken.None);
        (await slots.WaitedAsync(["a"], CancellationToken.None)).Should().BeEmpty("a parked or resumed run's clocks start again");
    }

    [Fact]
    public async Task Release_DropsTheRunsDoor_SoNoSemaphoreOutlivesItsRun()
    {
        var signal = new ModelRunSlotSignal();
        var door = signal.Entering("a");

        await new InProcessModelRunSlots(signal).ReleaseAsync("a", CancellationToken.None);

        signal.Entering("a").Should().NotBeSameAs(door);
    }
}

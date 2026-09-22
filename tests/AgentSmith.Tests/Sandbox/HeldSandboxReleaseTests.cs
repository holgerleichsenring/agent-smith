using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Sandbox;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>
/// 2026-09-22-2d11a: a hold can never deny a run, because every door that asks a
/// capacity probe releases what this process holds BEFORE it asks. The release is a
/// force remove with no grace — an ordinary disposal pushes a shutdown step and then
/// waits a flat ten seconds, serially, on a door an operator is waiting at.
/// <para>
/// Nothing holds anything until 2026-09-22-2d11b, so the register is driven directly
/// here — and the empty-register case is asserted to change nothing.
/// </para>
/// </summary>
public sealed class HeldSandboxReleaseTests
{
    [Fact]
    public async Task Release_ADoorWithReleasableHolds_ForceRemovesTheLeastRecentlyUsedBeforeProbing()
    {
        var register = NewRegister();
        var first = Held("scope-a");
        var second = Held("scope-b");
        register.Hold(first);
        register.Hold(second);
        // 'scope-a' becomes the more recently used of the two.
        register.Take(first.Key);
        register.Release(first.Key);

        var released = await register.EvictAsync(CancellationToken.None);

        released.Should().Be(2);
        Removal(second).ForceRemovedAt.Should().BeLessThan(Removal(first).ForceRemovedAt!.Value,
            "the least recently used hold goes first");
        Removal(first).Disposed.Should().BeFalse();
        Removal(second).Disposed.Should().BeFalse();
    }

    [Fact]
    public async Task Release_AHoldATurnHasTaken_IsNeverReleased()
    {
        var register = NewRegister();
        var taken = Held("scope-in-use");
        register.Hold(taken);
        register.Take(taken.Key).Should().BeTrue();

        var released = await register.EvictAsync(CancellationToken.None);

        released.Should().Be(0);
        Removal(taken).ForceRemovedAt.Should().BeNull(
            "a re-taken hold still carries the run label of the turn that spawned it, so no "
            + "reaper rail could tell it from an idle one — the register is what knows");
        register.Take(taken.Key).Should().BeFalse("it is already taken");
    }

    [Fact]
    public async Task Release_NoReleasePath_CallsTheTenSecondDisposal()
    {
        var register = NewRegister();
        var held = Held("scope-a");
        register.Hold(held);

        await register.EvictAsync(CancellationToken.None);

        Removal(held).Disposed.Should().BeFalse(
            "a release force-removes; the ten-second shutdown grace protects a step in "
            + "flight, and a held sandbox has none");
    }

    [Fact]
    public async Task Release_AnEmptyRegister_LeavesEveryAdmissionDecisionAsItIsToday()
    {
        var register = NewRegister();

        var released = await register.EvictAsync(CancellationToken.None);

        released.Should().Be(0, "nothing holds anything yet, so every door probes the world it always did");
    }

    [Fact]
    public async Task Release_AHoldThatCannotBeRemoved_LeavesTheRegisterAndIsLeftToTheReapers()
    {
        var register = NewRegister();
        var broken = new HeldSandbox("scope-a", "a1b2c3d4", new ThrowingRemoval());
        register.Hold(broken);

        (await register.EvictAsync(CancellationToken.None)).Should().Be(0);

        register.Take(broken.Key).Should().BeFalse("the hold is out of the register either way");
    }

    private static IHeldSandboxRegister NewRegister() =>
        new HeldSandboxRegister(NullLogger<HeldSandboxRegister>.Instance);

    private static HeldSandbox Held(string key) => new(key, "a1b2c3d4", new RecordingRemoval());

    private static RecordingRemoval Removal(HeldSandbox held) => (RecordingRemoval)held.Sandbox;

    private sealed class RecordingRemoval : ISandboxForceRemoval, IAsyncDisposable
    {
        private static int _order;

        public int? ForceRemovedAt { get; private set; }
        public bool Disposed { get; private set; }

        public Task ForceRemoveAsync(CancellationToken cancellationToken)
        {
            ForceRemovedAt = Interlocked.Increment(ref _order);
            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }

    private sealed class ThrowingRemoval : ISandboxForceRemoval
    {
        public Task ForceRemoveAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("the daemon is not reachable");
    }
}

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
/// 2026-09-22-2d11b: a take LEAVES the register, so an eviction running beside a turn
/// cannot pull a sandbox out from under the read in flight; the turn holds it again when
/// it ends. The empty-register case is still asserted to change nothing.
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
        (await register.TakeAsync(first.Key, CancellationToken.None)).Should().NotBeNull();
        register.Hold(first);

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
        (await register.TakeAsync(taken.Key, CancellationToken.None)).Should().NotBeNull();

        var released = await register.EvictAsync(CancellationToken.None);

        released.Should().Be(0);
        Removal(taken).ForceRemovedAt.Should().BeNull(
            "a re-taken hold still carries the run label of the turn that spawned it, so no "
            + "reaper rail could tell it from an idle one — the register is what knows");
        (await register.TakeAsync(taken.Key, CancellationToken.None)).Should()
            .BeNull("a turn already has it");
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

        released.Should().Be(0, "a process holding nothing releases nothing, and every door probes the world it always did");
    }

    [Fact]
    public async Task Release_AHoldThatCannotBeRemoved_LeavesTheRegisterAndIsLeftToTheReapers()
    {
        var register = NewRegister();
        var broken = new HeldSandbox(
            "scope-a", "a1b2c3d4", new FakeHoldableSandbox { ThrowOnForceRemove = true });
        register.Hold(broken);

        (await register.EvictAsync(CancellationToken.None)).Should().Be(0);

        (await register.TakeAsync(broken.Key, CancellationToken.None)).Should()
            .BeNull("the hold is out of the register either way");
    }

    private static IHeldSandboxRegister NewRegister() =>
        new HeldSandboxRegister(new StubHeartbeat(alive: true), NullLogger<HeldSandboxRegister>.Instance);

    private static HeldSandbox Held(string key) => new(key, "a1b2c3d4", new FakeHoldableSandbox());

    private static FakeHoldableSandbox Removal(HeldSandbox held) => (FakeHoldableSandbox)held.Sandbox;
}

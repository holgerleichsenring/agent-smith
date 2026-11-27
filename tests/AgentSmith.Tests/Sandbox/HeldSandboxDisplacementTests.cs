using AgentSmith.Contracts.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-27-3532c: two holders of one key (a series beat and a design turn reading the same upload) each put
/// their own sandbox back; the last one stays held and the one it displaced is removed, never orphaned.</summary>
public sealed class HeldSandboxDisplacementTests
{
    [Fact]
    public async Task Hold_AKeyHoldingAnotherSandbox_RemovesTheDisplacedOne()
    {
        var register = Holds.Live();
        var first = new HeldSandbox("reference:site", "conv-1", new FakeHoldableSandbox());
        var second = new HeldSandbox("reference:site", "conv-1", new FakeHoldableSandbox());

        register.Hold(first);
        register.Hold(second);

        ((FakeHoldableSandbox)first.Sandbox).ForceRemovedAt.Should().NotBeNull("the displaced sandbox is removed");
        ((FakeHoldableSandbox)second.Sandbox).ForceRemovedAt.Should().BeNull();
        (await register.TakeAsync("reference:site", CancellationToken.None)).Should().BeSameAs(second.Sandbox);
    }

    [Fact]
    public void Hold_TheSameSandboxAgain_RemovesNothing()
    {
        var register = Holds.Live();
        var held = new HeldSandbox("reference:site", "conv-1", new FakeHoldableSandbox());

        register.Hold(held);
        register.Hold(held);

        ((FakeHoldableSandbox)held.Sandbox).ForceRemovedAt.Should().BeNull();
    }
}

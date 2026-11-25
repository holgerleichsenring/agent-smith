using AgentSmith.Application.Services.Sandbox;
using FluentAssertions;

namespace AgentSmith.Tests.Sandbox;

/// <summary>2026-11-25-9992b: steps share the workspace's gate; a restart holds it alone; its own steps pass. Every
/// path that does not wait completes synchronously, so the tests read completion instead of waiting on a clock.</summary>
public sealed class WorkspaceStepGateTests
{
    [Fact]
    public async Task EnterAsync_TwoSteps_RunSideBySide()
    {
        var gate = new WorkspaceStepGate();

        using var first = await gate.EnterAsync(CancellationToken.None);
        var second = gate.EnterAsync(CancellationToken.None);

        second.IsCompletedSuccessfully.Should().BeTrue("nothing holds the gate alone");
        (await second).Dispose();
    }

    [Fact]
    public async Task RunAloneAsync_AStepInFlight_WaitsUntilItEnds()
    {
        var gate = new WorkspaceStepGate();
        var step = await gate.EnterAsync(CancellationToken.None);
        var ran = false;

        var alone = gate.RunAloneAsync(() => { ran = true; return Task.FromResult(1); }, CancellationToken.None);
        ran.Should().BeFalse("a step was still running");
        alone.IsCompleted.Should().BeFalse();
        step.Dispose();

        (await alone).Should().Be(1);
        ran.Should().BeTrue();
    }

    [Fact]
    public async Task EnterAsync_WhileARestartHoldsTheGate_WaitsForIt()
    {
        var gate = new WorkspaceStepGate();
        var release = new TaskCompletionSource();
        var alone = gate.RunAloneAsync(async () => { await release.Task; return 0; }, CancellationToken.None);

        var step = gate.EnterAsync(CancellationToken.None);
        step.IsCompleted.Should().BeFalse("the restart holds the gate");
        release.SetResult();
        await alone;

        (await step).Dispose();
    }

    [Fact]
    public async Task EnterAsync_FromTheRestartsOwnFlow_Passes()
    {
        var gate = new WorkspaceStepGate();

        var passed = await gate.RunAloneAsync(() =>
        {
            var own = gate.EnterAsync(CancellationToken.None);
            own.Result.Dispose();
            return Task.FromResult(own.IsCompletedSuccessfully);
        }, CancellationToken.None);

        passed.Should().BeTrue();
    }
}

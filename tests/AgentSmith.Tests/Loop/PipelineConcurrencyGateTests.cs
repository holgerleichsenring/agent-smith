using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class PipelineConcurrencyGateTests
{
    /// <summary>
    /// 2026-08-28-3793: a LIVENESS bound, not a latency one. The claim is that a released
    /// permit reaches the waiter at all; the wait exists so a permit that never arrives
    /// fails the test instead of hanging the run. It was one second, and one second is a
    /// promise about the SCHEDULER — under a suite that runs three test projects at once
    /// an async continuation can wait longer than that with nothing wrong, which is the
    /// starvation MigratedStoreTemplate documents. Thirty seconds still catches a permit
    /// that is never released, and stops reporting a busy machine as a defect.
    /// </summary>
    private static readonly TimeSpan ReachesTheWaiter = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task AcquireAsync_BelowLimit_ReturnsImmediately()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 2 });

        using var permit = await gate.AcquireAsync(CancellationToken.None);

        permit.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_AboveLimit_BlocksUntilRelease()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });

        var permit1 = await gate.AcquireAsync(CancellationToken.None);
        var task = gate.AcquireAsync(CancellationToken.None);

        task.IsCompleted.Should().BeFalse();
        permit1.Dispose();

        var permit2 = await task.WaitAsync(ReachesTheWaiter);
        permit2.Should().NotBeNull();
        permit2.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_DisposeReleasesPermit()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });

        var permit = await gate.AcquireAsync(CancellationToken.None);
        permit.Dispose();

        var second = await gate.AcquireAsync(CancellationToken.None).WaitAsync(ReachesTheWaiter);
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_RespectsCancellationToken()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });
        await gate.AcquireAsync(CancellationToken.None);

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(50);

        Func<Task> act = async () => await gate.AcquireAsync(cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

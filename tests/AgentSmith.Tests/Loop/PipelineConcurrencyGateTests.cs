using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

public sealed class PipelineConcurrencyGateTests
{
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

        // 2026-08-28-3793 wrote a LIVENESS bound here and 2026-09-22-3f7c took the number off
        // it: one second became thirty because a busy scheduler is not a defect, and thirty is
        // the same promise about the scheduler with a bigger number in it. What is being
        // claimed is that the released permit reaches the waiter AT ALL.
        var permit2 = await task.OrHang("the released permit reaches the waiter");
        permit2.Should().NotBeNull();
        permit2.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_DisposeReleasesPermit()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });

        var permit = await gate.AcquireAsync(CancellationToken.None);
        permit.Dispose();

        var second = await gate.AcquireAsync(CancellationToken.None)
            .OrHang("the disposed permit is available again");
        second.Should().NotBeNull();
    }

    [Fact]
    public async Task AcquireAsync_RespectsCancellationToken()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });
        await gate.AcquireAsync(CancellationToken.None);

        // The waiter is cancelled by the test rather than by a timer, so what is proven is
        // that a cancelled wait throws — not that fifty milliseconds were enough for it to
        // have started waiting.
        using var cts = new CancellationTokenSource();
        var waiting = gate.AcquireAsync(cts.Token);
        await cts.CancelAsync();

        Func<Task> act = async () => await waiting;
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}

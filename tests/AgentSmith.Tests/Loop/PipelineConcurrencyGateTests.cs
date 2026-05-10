using AgentSmith.Application.Services.Loop;
using AgentSmith.Contracts.Models.Configuration;
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

        var permit2 = await task.WaitAsync(TimeSpan.FromSeconds(1));
        permit2.Should().NotBeNull();
        permit2.Dispose();
    }

    [Fact]
    public async Task AcquireAsync_DisposeReleasesPermit()
    {
        using var gate = new PipelineConcurrencyGate(new LoopLimitsConfig { MaxConcurrentSkillCalls = 1 });

        var permit = await gate.AcquireAsync(CancellationToken.None);
        permit.Dispose();

        var second = await gate.AcquireAsync(CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(1));
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

using AgentSmith.Application.Services.Loop;
using FluentAssertions;

namespace AgentSmith.Tests.Loop;

/// <summary>2026-11-17-274ca: a scout call past its bound says which value and key stopped it.</summary>
public sealed class ScoutCallTimeBoundTests
{
    [Fact]
    public async Task RunAsync_CallOutlivesTheBound_ThrowsTimeoutNamingKeyAndValue()
    {
        var act = () => ScoutCallTimeBound.RunAsync(0, Forever, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<TimeoutException>();
        thrown.Which.Message.Should().Contain("limits.max_seconds_per_skill_call = 0 s").And.Contain("wall-clock bound");
    }

    [Fact]
    public async Task RunAsync_OuterCancellation_StaysACancellation()
    {
        using var outer = new CancellationTokenSource();
        await outer.CancelAsync();

        var act = () => ScoutCallTimeBound.RunAsync(300, Forever, outer.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task RunAsync_CallFinishes_ReturnsItsResult() =>
        (await ScoutCallTimeBound.RunAsync(300, _ => Task.FromResult(7), CancellationToken.None)).Should().Be(7);

    private static async Task<int> Forever(CancellationToken ct)
    {
        await Task.Delay(Timeout.InfiniteTimeSpan, ct);
        return 0;
    }
}

using AgentSmith.Application.Services.Loop;
using FluentAssertions;

namespace AgentSmith.Tests.SubAgents;

public sealed class SubAgentBudgetTests
{
    [Fact]
    public void SubAgentBudget_RespectsMaxPerRunAcrossMultipleSpawnCalls()
    {
        var budget = new SubAgentBudget(maxPerRun: 5);
        budget.TryReserve(3).Should().Be(3);
        budget.TryReserve(3).Should().Be(2);
        budget.TryReserve(3).Should().Be(0);
        budget.Used.Should().Be(5);
        budget.Remaining.Should().Be(0);
    }

    [Fact]
    public void SubAgentBudget_OverBudget_ReturnsFewerOrZero_NeverThrows()
    {
        var budget = new SubAgentBudget(maxPerRun: 2);
        budget.TryReserve(10).Should().Be(2);
        budget.TryReserve(10).Should().Be(0);
        var act = () => budget.TryReserve(int.MaxValue);
        act.Should().NotThrow();
    }

    [Fact]
    public async Task SubAgentBudget_ConcurrentReserve_NoOversubscription()
    {
        var budget = new SubAgentBudget(maxPerRun: 100);
        var totalGranted = 0;
        var tasks = Enumerable.Range(0, 50).Select(_ => Task.Run(() =>
        {
            var granted = budget.TryReserve(5);
            Interlocked.Add(ref totalGranted, granted);
        })).ToArray();
        await Task.WhenAll(tasks);

        totalGranted.Should().Be(100);
        budget.Used.Should().Be(100);
    }
}

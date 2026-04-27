using AgentSmith.Application.Services;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.Tests.Services;

public class PipelineCostTrackerConcurrencyTests
{
    [Fact]
    public async Task Track_ConcurrentCallsFromManyTasks_NoLostUpdates()
    {
        var tracker = new PipelineCostTracker();
        const int taskCount = 100;
        const int callsPerTask = 50;

        var tasks = Enumerable.Range(0, taskCount).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < callsPerTask; i++)
            {
                tracker.Track(new LlmResponse(
                    Text: "x",
                    InputTokens: 10,
                    OutputTokens: 5,
                    Model: "claude-sonnet-4-20250514"));
            }
        })).ToArray();

        await Task.WhenAll(tasks);

        tracker.CallCount.Should().Be(taskCount * callsPerTask);
        tracker.TotalInputTokens.Should().Be(taskCount * callsPerTask * 10);
        tracker.TotalOutputTokens.Should().Be(taskCount * callsPerTask * 5);
    }

    [Fact]
    public async Task Track_ConcurrentReadsAndWrites_DoesNotThrow()
    {
        var tracker = new PipelineCostTracker();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var writers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
                tracker.Track(new LlmResponse("x", 1, 1, "claude-sonnet-4-20250514"));
        }, cts.Token)).ToArray();

        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var calls = tracker.CallCount;
                var cost = tracker.EstimateCostUsd();
                var summary = tracker.ToString();
                if (calls < 0 || cost < 0 || summary.Length == 0) throw new InvalidOperationException();
            }
        }, cts.Token)).ToArray();

        var act = async () =>
        {
            try { await Task.WhenAll(writers.Concat(readers)); }
            catch (OperationCanceledException) { /* expected */ }
        };

        await act.Should().NotThrowAsync();
    }
}

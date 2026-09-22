using AgentSmith.Sandbox.Wire;
using AgentSmith.Sandbox.Agent.Services;
using AgentSmith.Tests.TestHelpers;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class OutputBatcherTests
{
    // 2026-09-22-3f7c: the batcher flushes from its own timer thread, so these tests wait for
    // the FLUSH rather than for a stretch of clock in which one was likely — and they collect
    // it under a lock, because a poll must never race the producer it polls.
    [Fact]
    public async Task Add_BelowThreshold_FlushesOnTimer()
    {
        var batches = new Flushes();
        await using var batcher = new OutputBatcher(50, TimeSpan.FromMilliseconds(50), batches.Record);

        batcher.Add(MakeEvent("a"));

        await TestWaits.UntilAsync(() => batches.Lines().Contains("a"), "the timer flushes the event");
    }

    [Fact]
    public async Task Add_AtThreshold_TriggersImmediateFlush()
    {
        var batches = new Flushes();
        await using var batcher = new OutputBatcher(5, TimeSpan.FromSeconds(10), batches.Record);

        for (var i = 0; i < 5; i++) batcher.Add(MakeEvent($"line{i}"));

        await TestWaits.UntilAsync(() => batches.All().Count == 1, "the threshold flushes once");
        batches.All()[0].Should().HaveCount(5);
    }

    [Fact]
    public async Task DisposeAsync_FlushesPendingEvents()
    {
        var batches = new Flushes();
        var batcher = new OutputBatcher(50, TimeSpan.FromSeconds(10), batches.Record);

        batcher.Add(MakeEvent("trailing"));
        await batcher.DisposeAsync();

        batches.Lines().Should().Contain("trailing");
    }

    private sealed class Flushes
    {
        private readonly List<IReadOnlyList<StepEvent>> _batches = [];

        public Task Record(IReadOnlyList<StepEvent> batch)
        {
            lock (_batches) _batches.Add(batch);
            return Task.CompletedTask;
        }

        public IReadOnlyList<IReadOnlyList<StepEvent>> All()
        {
            lock (_batches) return [.. _batches];
        }

        public IReadOnlyList<string> Lines() => [.. All().SelectMany(b => b).Select(e => e.Line)];
    }

    private static StepEvent MakeEvent(string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.NewGuid(), StepEventKind.Stdout, line, DateTimeOffset.UtcNow);
}

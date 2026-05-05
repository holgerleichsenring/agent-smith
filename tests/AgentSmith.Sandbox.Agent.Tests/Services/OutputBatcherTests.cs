using AgentSmith.Sandbox.Wire;
using AgentSmith.Sandbox.Agent.Services;
using FluentAssertions;

namespace AgentSmith.Sandbox.Agent.Tests.Services;

public class OutputBatcherTests
{
    [Fact]
    public async Task Add_BelowThreshold_FlushesOnTimer()
    {
        var batches = new List<IReadOnlyList<StepEvent>>();
        await using var batcher = new OutputBatcher(50, TimeSpan.FromMilliseconds(50),
            batch => { batches.Add(batch); return Task.CompletedTask; });

        batcher.Add(MakeEvent("a"));
        await Task.Delay(200);

        batches.Should().NotBeEmpty();
        batches.SelectMany(b => b).Select(e => e.Line).Should().Contain("a");
    }

    [Fact]
    public async Task Add_AtThreshold_TriggersImmediateFlush()
    {
        var batches = new List<IReadOnlyList<StepEvent>>();
        await using var batcher = new OutputBatcher(5, TimeSpan.FromSeconds(10),
            batch => { batches.Add(batch); return Task.CompletedTask; });

        for (var i = 0; i < 5; i++) batcher.Add(MakeEvent($"line{i}"));
        await Task.Delay(100);

        batches.Should().HaveCount(1);
        batches[0].Should().HaveCount(5);
    }

    [Fact]
    public async Task DisposeAsync_FlushesPendingEvents()
    {
        var batches = new List<IReadOnlyList<StepEvent>>();
        var batcher = new OutputBatcher(50, TimeSpan.FromSeconds(10),
            batch => { batches.Add(batch); return Task.CompletedTask; });

        batcher.Add(MakeEvent("trailing"));
        await batcher.DisposeAsync();

        batches.Should().NotBeEmpty();
        batches.SelectMany(b => b).Select(e => e.Line).Should().Contain("trailing");
    }

    private static StepEvent MakeEvent(string line) =>
        new(StepEvent.CurrentSchemaVersion, Guid.NewGuid(), StepEventKind.Stdout, line, DateTimeOffset.UtcNow);
}

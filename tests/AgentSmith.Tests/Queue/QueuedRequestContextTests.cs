using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Queue;

/// <summary>2026-11-18-550aa: a request's strings and flags read as sent after the JSON round trip.</summary>
public sealed class QueuedRequestContextTests
{
    [Fact]
    public void Restore_SerializedRequest_ReadsStringsAndFlagsAsSent()
    {
        var sent = new PipelineRequest("p1", "series-run", Context: new()
        {
            [ContextKeys.SeriesRunId] = "sr-1", [ContextKeys.RefreshPrinciples] = true,
            ["Off"] = false, ["Count"] = 3, ["Shape"] = new Dictionary<string, string> { ["a"] = "b" },
        });

        var restored = QueuedRequestContext.Restore(
            JsonSerializer.Deserialize<PipelineRequest>(JsonSerializer.Serialize(sent))!);

        var pipeline = new PipelineContext();
        foreach (var (key, value) in restored.Context!) pipeline.Set(key, value);
        pipeline.TryGet<string>(ContextKeys.SeriesRunId, out var series).Should().BeTrue();
        series.Should().Be("sr-1");
        pipeline.TryGet<bool>(ContextKeys.RefreshPrinciples, out var flag).Should().BeTrue();
        flag.Should().BeTrue();
        restored.Context!["Off"].Should().Be(false);
        restored.Context["Count"].Should().BeOfType<JsonElement>("a number stays for its reader to parse");
        restored.Context["Shape"].Should().BeOfType<JsonElement>();
    }

    [Fact]
    public void Restore_NoContext_ReturnsTheRequest()
    {
        var request = new PipelineRequest("p1", "code");

        QueuedRequestContext.Restore(request).Should().BeSameAs(request);
    }
}

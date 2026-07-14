using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using FluentAssertions;

namespace AgentSmith.PipelineHarness.Composition;

/// <summary>The Redis job-list boundary: records enqueued requests and hands
/// them back through the SAME JSON round-trip RedisJobQueue performs, so
/// resume-context values arrive as JsonElement exactly like production.
/// Shared by the p0327 durable-dialogue and p0328 expectation tests.</summary>
public sealed class RecordingJobQueue : IRedisJobQueue
{
    private readonly List<string> _enqueued = [];

    public Task EnqueueAsync(PipelineRequest request, CancellationToken cancellationToken)
    {
        _enqueued.Add(System.Text.Json.JsonSerializer.Serialize(request));
        return Task.CompletedTask;
    }

    public PipelineRequest DequeueViaJsonRoundTrip()
    {
        _enqueued.Should().NotBeEmpty("the pump must have enqueued the resume request");
        var json = _enqueued[^1];
        return System.Text.Json.JsonSerializer.Deserialize<PipelineRequest>(json)!;
    }

    public async IAsyncEnumerable<PipelineRequest> ConsumeAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        await Task.CompletedTask;
        yield break;
    }

    public Task<long> LenAsync(CancellationToken cancellationToken) =>
        Task.FromResult((long)_enqueued.Count);
}

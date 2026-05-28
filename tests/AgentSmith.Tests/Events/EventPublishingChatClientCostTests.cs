using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Events;

public sealed class EventPublishingChatClientCostTests
{
    private const string RunId = "2026-05-28T09-00-00-cccc";

    [Fact]
    public async Task GetResponseAsync_EmitsLlmCallFinishedWithRealCostFromPricing()
    {
        var recorder = new RecordingPublisher();
        var resolver = StubResolver(input: 2m, output: 8m, cacheRead: 0m);
        var client = NewClient(
            new StubChat("gpt-4.1-2025-04-14", input: 1_000_000, output: 1_000_000),
            recorder, resolver);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") }, options: null, CancellationToken.None);

        var finished = recorder.Events.OfType<LlmCallFinishedEvent>().Single();
        finished.CostUsd.Should().Be(10.0m); // 1M * 2 + 1M * 8
        finished.TokensIn.Should().Be(1_000_000);
        finished.TokensOut.Should().Be(1_000_000);
    }

    [Fact]
    public async Task ResponseWithCacheReadTokens_CostExcludesBilledCacheReads()
    {
        var recorder = new RecordingPublisher();
        var resolver = StubResolver(input: 3m, output: 15m, cacheRead: 0.30m);
        var stub = new StubChat("claude-sonnet-4-20250514", input: 1_000_000, output: 0)
        {
            CacheReadTokens = 800_000,
        };
        var client = NewClient(stub, recorder, resolver);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") }, options: null, CancellationToken.None);

        var finished = recorder.Events.OfType<LlmCallFinishedEvent>().Single();
        // billable input = 1M - 800k = 200k → 200k/1M * 3 = 0.6
        // cache_read 800k → 800k/1M * 0.30 = 0.24
        // total = 0.6 + 0.24 = 0.84
        finished.CostUsd.Should().Be(0.84m);
    }

    private static IModelPricingResolver StubResolver(decimal input, decimal output, decimal cacheRead)
        => new ModelPricingResolver(new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4.1"] = new() { InputPerMillion = input, OutputPerMillion = output, CacheReadPerMillion = cacheRead },
            ["claude-sonnet-4-20250514"] = new() { InputPerMillion = input, OutputPerMillion = output, CacheReadPerMillion = cacheRead },
        });

    private static EventPublishingChatClient NewClient(
        IChatClient inner, IEventPublisher publisher, IModelPricingResolver resolver) =>
        new(inner, publisher, new ScopedRunContext(RunId), resolver, role: "Lead");

    private sealed class StubChat : IChatClient
    {
        private readonly string _modelId;
        private readonly long _input;
        private readonly long _output;

        public StubChat(string modelId, long input, long output)
        {
            _modelId = modelId;
            _input = input;
            _output = output;
        }

        public long CacheReadTokens { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var usage = new UsageDetails { InputTokenCount = _input, OutputTokenCount = _output };
            if (CacheReadTokens > 0)
                usage.AdditionalCounts = new AdditionalPropertiesDictionary<long>
                {
                    ["cache_read_input_tokens"] = CacheReadTokens
                };
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                ModelId = _modelId,
                Usage = usage,
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class RecordingPublisher : IEventPublisher
    {
        public List<RunEvent> Events { get; } = new();

        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            Events.Add(runEvent);
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public IDisposable BeginScope(string id) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }
}

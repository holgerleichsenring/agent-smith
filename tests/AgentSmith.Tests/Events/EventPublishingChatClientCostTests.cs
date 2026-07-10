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
    public async Task ResponseWithAnthropicCacheReadTokens_BillsThemAtCacheReadRate()
    {
        // p0323: models the REAL Anthropic.SDK 5.10.0 adapter shape — PascalCase
        // AdditionalCounts keys, and InputTokenCount is the UNCACHED remainder
        // (Anthropic's input_tokens excludes cache reads), so nothing is subtracted.
        var recorder = new RecordingPublisher();
        var resolver = StubResolver(input: 3m, output: 15m, cacheRead: 0.30m);
        var stub = new StubChat("claude-sonnet-4-20250514", input: 200_000, output: 0)
        {
            AdditionalCounts = new() { ["CacheReadInputTokens"] = 800_000 },
        };
        var client = NewClient(stub, recorder, resolver);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") }, options: null, CancellationToken.None);

        var finished = recorder.Events.OfType<LlmCallFinishedEvent>().Single();
        // billable input = 200k (already excludes cache reads) → 200k/1M * 3 = 0.6
        // cache_read 800k → 800k/1M * 0.30 = 0.24
        // total = 0.6 + 0.24 = 0.84
        finished.CostUsd.Should().Be(0.84m);
        finished.CachedTokensIn.Should().Be(800_000, "the cached share must be observable per call");
        finished.CacheCreationTokensIn.Should().Be(0);
    }

    [Fact]
    public async Task ResponseWithOpenAiCachedTokens_SubtractsThemFromBillableInput()
    {
        // p0323: OpenAI reports the input TOTAL including the cached subset
        // ('cached_tokens'), so billable = total - cached.
        var recorder = new RecordingPublisher();
        var resolver = StubResolver(input: 2m, output: 8m, cacheRead: 0.50m);
        var stub = new StubChat("gpt-4.1", input: 1_000_000, output: 0)
        {
            AdditionalCounts = new() { ["cached_tokens"] = 500_000 },
        };
        var client = NewClient(stub, recorder, resolver);

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hello") }, options: null, CancellationToken.None);

        var finished = recorder.Events.OfType<LlmCallFinishedEvent>().Single();
        // billable = 1M - 500k = 500k → 500k/1M * 2 = 1.0; cached 500k * 0.5/1M = 0.25
        finished.CostUsd.Should().Be(1.25m);
        finished.CachedTokensIn.Should().Be(500_000);
    }

    private static IModelPricingResolver StubResolver(decimal input, decimal output, decimal cacheRead)
        => new ModelPricingResolver(new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
        {
            ["gpt-4.1"] = new() { InputPerMillion = input, OutputPerMillion = output, CacheReadPerMillion = cacheRead },
            ["claude-sonnet-4-20250514"] = new() { InputPerMillion = input, OutputPerMillion = output, CacheReadPerMillion = cacheRead },
        });

    private static EventPublishingChatClient NewClient(
        IChatClient inner, IEventPublisher publisher, IModelPricingResolver resolver)
    {
        var ctx = new ScopedRunContext(RunId);
        ctx.BeginCallScope("Lead", "Plan");
        return new EventPublishingChatClient(inner, publisher, ctx, resolver);
    }

    [Fact]
    public async Task StartedEvent_UsesConfiguredModel_WhenOptionsHasNoModelId()
    {
        // p0224: the in-flight row must show the real model, not "unknown".
        var recorder = new RecordingPublisher();
        var ctx = new ScopedRunContext(RunId);
        ctx.BeginCallScope("project-analyzer", "Analyze");
        var client = new EventPublishingChatClient(
            new StubChat("gpt-4.1-2025-04-14", input: 10, output: 5),
            recorder, ctx, StubResolver(1m, 1m, 0m), "gpt-4.1-2025-04-14");

        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") }, options: null, CancellationToken.None);

        var started = recorder.Events.OfType<LlmCallStartedEvent>().Single();
        started.Model.Should().Be("gpt-4.1-2025-04-14");
    }

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

        public Dictionary<string, long>? AdditionalCounts { get; set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            var usage = new UsageDetails { InputTokenCount = _input, OutputTokenCount = _output };
            if (AdditionalCounts is { Count: > 0 })
            {
                usage.AdditionalCounts = new AdditionalPropertiesDictionary<long>();
                foreach (var (key, value) in AdditionalCounts)
                    usage.AdditionalCounts[key] = value;
            }
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
        public CallScope? CurrentCallScope => callScope;
        public IDisposable BeginScope(string id) => new NoOpScope();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null)
        {
            callScope = new CallScope(role, phase, repoName);
            return new NoOpScope();
        }
        private CallScope? callScope;
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }
}

using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Events;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Workers;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Events;

/// <summary>
/// 2026-11-17-7c9bc: every LlmCallFinished the client publishes lands in the run's ambient call ledger with
/// the same figures — a worker call with its CLI-reported cost, a provider call with its table price.
/// </summary>
public sealed class RecordedCallLedgerFeedTests
{
    [Fact]
    public async Task GetResponseAsync_WorkerAndProviderCalls_LandInTheAmbientLedgerAsPublished()
    {
        var context = new AsyncLocalRunContextAccessor();
        var ledger = new RecordedCallLedger();
        var published = new List<LlmCallFinishedEvent>();
        using var run = context.BeginScope("run-1");
        using var calls = context.BeginRecordedCallsScope(ledger);
        using var call = context.BeginCallScope("Lead", "Plan", "primary");

        await Client(context, published, Answer("gpt-4.1", worker: null)).GetResponseAsync([new(ChatRole.User, "a")]);
        await Client(context, published, Answer("claude-opus-5-5",
            new WorkerCallAccounting("claude-opus-5-5", 2, 900, 12_149, 57_017, 0.4213m, 1))).GetResponseAsync([new(ChatRole.User, "b")]);

        ledger.Calls.Should().HaveCount(2);
        ledger.Calls.Select(c => c.CostUsd).Should().Equal(published.Select(e => e.CostUsd));
        ledger.Calls[1].Should().Be(new RecordedCall(
            "claude-opus-5-5", "Plan", "primary", 2, 900, 12_149, 57_017, 0.4213m, IsWorker: true));
        ledger.Calls[0].IsWorker.Should().BeFalse();
        ledger.CostUsd.Should().Be(published.Sum(e => e.CostUsd));
    }

    [Fact]
    public async Task GetResponseAsync_NoLedgerFrame_PublishesAsBefore()
    {
        var context = new AsyncLocalRunContextAccessor();
        var published = new List<LlmCallFinishedEvent>();
        using var run = context.BeginScope("run-1");

        await Client(context, published, Answer("gpt-4.1", worker: null)).GetResponseAsync([new(ChatRole.User, "a")]);

        published.Should().ContainSingle();
        context.CurrentRecordedCalls.Should().BeNull();
    }

    private static EventPublishingChatClient Client(
        IRunContextAccessor context, List<LlmCallFinishedEvent> published, ChatResponse answer) =>
        new(new Answering(answer), new Capturing(published), context,
            new LlmCallCostCalculator(new ModelPricingResolver(new Dictionary<string, ModelPricing>(StringComparer.OrdinalIgnoreCase)
            {
                ["gpt-4.1"] = new() { InputPerMillion = 2m, OutputPerMillion = 8m },
            })),
            new ThrottleWaitReporter());

    private static ChatResponse Answer(string model, WorkerCallAccounting? worker)
    {
        var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = model,
            Usage = new UsageDetails { InputTokenCount = 1_000_000, OutputTokenCount = 1_000_000 },
        };
        return worker?.AttachTo(response) ?? response;
    }

    private sealed class Answering(ChatResponse answer) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(answer);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }

    private sealed class Capturing(List<LlmCallFinishedEvent> published) : IEventPublisher
    {
        public Task PublishAsync(RunEvent runEvent, CancellationToken cancellationToken = default)
        {
            if (runEvent is LlmCallFinishedEvent finished) published.Add(finished);
            return Task.CompletedTask;
        }
    }
}

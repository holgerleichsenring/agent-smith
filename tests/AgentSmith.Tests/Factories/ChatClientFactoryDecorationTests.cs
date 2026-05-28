using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Infrastructure.Services.Factories;
using AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Factories;

public sealed class ChatClientFactoryDecorationTests
{
    private const string RunId = "2026-05-28T09-00-00-fact";

    [Fact]
    public async Task Create_ReturnsClientWrappedInEventPublishingChain()
    {
        var recorder = new RecordingPublisher();
        var factory = NewFactory(recorder);
        var agent = new AgentConfig
        {
            Type = "stub",
            Model = "gpt-4.1",
            Models = new ModelRegistryConfig
            {
                Primary = new() { Model = "gpt-4.1", Deployment = "stub" },
                Scout = new() { Model = "gpt-4.1", Deployment = "stub" },
                Planning = new() { Model = "gpt-4.1", Deployment = "stub" },
                Reasoning = new() { Model = "gpt-4.1", Deployment = "stub" },
                Summarization = new() { Model = "gpt-4.1", Deployment = "stub" },
                ContextGeneration = new() { Model = "gpt-4.1", Deployment = "stub" },
                CodeMapGeneration = new() { Model = "gpt-4.1", Deployment = "stub" },
            },
        };

        // Non-tool-bearing path so FunctionInvokingChatClient doesn't wrap;
        // we get EventPublishingChatClient directly.
        var client = factory.Create(agent, TaskType.Summarization);
        await client.GetResponseAsync(
            new[] { new ChatMessage(ChatRole.User, "hi") }, options: null, CancellationToken.None);

        recorder.Events.OfType<LlmCallStartedEvent>().Should().HaveCount(1);
        recorder.Events.OfType<LlmCallFinishedEvent>().Should().HaveCount(1);
    }

    private static ChatClientFactory NewFactory(IEventPublisher publisher)
        => new(
            new[] { new StubBuilder() },
            publisher,
            new ScopedRunContext(RunId),
            new ModelPricingResolver(),
            NullLoggerFactory.Instance);

    private sealed class StubBuilder : IChatClientBuilder
    {
        public IReadOnlyList<string> SupportedTypes { get; } = new[] { "stub" };

        public IChatClient Build(AgentConfig agent, ModelAssignment assignment) => new StubChat();
    }

    private sealed class StubChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
            {
                ModelId = "gpt-4.1-2025-04-14",
                Usage = new UsageDetails { InputTokenCount = 100, OutputTokenCount = 50 },
            });

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
        public CallScope? CurrentCallScope => null;
        public IDisposable BeginScope(string id) => new NoOpScope();
        public IDisposable BeginCallScope(string role, string phase, string? repoName = null) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }
}

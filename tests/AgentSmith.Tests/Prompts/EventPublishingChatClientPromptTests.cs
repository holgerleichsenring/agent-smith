using AgentSmith.Application.Services;
using AgentSmith.Contracts.Events;
using AgentSmith.Contracts.Prompts;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Services.Events;
using AgentSmith.Infrastructure.Services.RateLimiting;
using AgentSmith.Tests.Events;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Prompts;

/// <summary>2026-11-09-0620a: the finished event names the stored prompt; a store that fails never fails the call.</summary>
public sealed class EventPublishingChatClientPromptTests
{
    [Fact]
    public async Task EventPublishingChatClient_Call_CarriesPromptIdAndToolCountOnFinished()
    {
        var publisher = new RecordingEventPublisher();
        var recorder = new KeepingRecorder();
        var tool = AIFunctionFactory.Create((string path) => path, "read_file", "Reads a file.");

        await Client(publisher, recorder).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")], new ChatOptions { Tools = [tool] });

        var finished = publisher.Events.OfType<LlmCallFinishedEvent>().Single();
        finished.PromptId.Should().Be("p-1");
        finished.ToolCount.Should().Be(1);
        recorder.Blocks.Should().HaveCount(2);
    }

    [Fact]
    public async Task EventPublishingChatClient_RecorderThrows_CallStillSucceedsWithoutPromptId()
    {
        var publisher = new RecordingEventPublisher();

        var response = await Client(publisher, new ThrowingRecorder()).GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hello")]);

        response.Text.Should().Be("ok");
        publisher.Events.OfType<LlmCallFinishedEvent>().Single().PromptId.Should().BeNull();
    }

    private static EventPublishingChatClient Client(RecordingEventPublisher publisher, IPromptRecorder recorder) =>
        new(new OkChat(), publisher, new ScopedRunContext("2026-11-09T22-00-00-cccc"),
            new LlmCallCostCalculator(new ModelPricingResolver()), new ThrottleWaitReporter(), prompts: recorder);

    private sealed class KeepingRecorder : IPromptRecorder
    {
        public IReadOnlyList<PromptBlock> Blocks { get; private set; } = [];

        public Task<string?> RecordAsync(string runId, IReadOnlyList<PromptBlock> blocks, int toolCount, CancellationToken ct)
        {
            Blocks = blocks;
            return Task.FromResult<string?>("p-1");
        }
    }

    private sealed class ThrowingRecorder : IPromptRecorder
    {
        public Task<string?> RecordAsync(string runId, IReadOnlyList<PromptBlock> blocks, int toolCount, CancellationToken ct) =>
            throw new InvalidOperationException("store down");
    }

    private sealed class ScopedRunContext(string runId) : IRunContextAccessor
    {
        public string? CurrentRunId => runId;
        public CallScope? CurrentCallScope => null;
        public IDisposable BeginScope(string id) => new NoOpScope();
        public int? CurrentStepIndex => null;
        public string? CurrentSpecId => null;
        public IDisposable BeginStepScope(int stepIndex, string? specId = null) => new NoOpScope();
        public IDisposable BeginCallScope(string role, string stage, string? repoName = null) => new NoOpScope();
        private sealed class NoOpScope : IDisposable { public void Dispose() { } }
    }

    private sealed class OkChat : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose() { }
    }
}

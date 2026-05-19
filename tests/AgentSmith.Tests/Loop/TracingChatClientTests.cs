using AgentSmith.Application.Models;
using AgentSmith.Application.Services.Loop;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Loop;

public sealed class TracingChatClientTests
{
    private sealed class StubChat(ChatResponse response) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(response);
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotImplementedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private static ChatResponse Response(string modelId = "gpt-4.1", int input = 100, int output = 50) =>
        new(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            ModelId = modelId,
            Usage = new UsageDetails { InputTokenCount = input, OutputTokenCount = output }
        };

    [Fact]
    public async Task GetResponseAsync_RecordsLlmEntryWithModelAndUsage()
    {
        var trace = new LoopTraceCollector();
        var inner = new StubChat(Response("model-x", input: 200, output: 75));
        var client = new TracingChatClient(inner, trace);

        await client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        var entries = trace.Build();
        entries.Should().HaveCount(1);
        entries[0].Kind.Should().Be(LoopTraceEntryKind.LlmCall);
        entries[0].ModelName.Should().Be("model-x");
        entries[0].InputTokens.Should().Be(200);
        entries[0].OutputTokens.Should().Be(75);
    }

    [Fact]
    public async Task GetResponseAsync_DelegatesToInner_ReturningInnerResponse()
    {
        var trace = new LoopTraceCollector();
        var expected = Response();
        var client = new TracingChatClient(new StubChat(expected), trace);

        var actual = await client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        actual.Should().BeSameAs(expected);
    }

    [Fact]
    public async Task GetResponseAsync_ResponseWithoutUsage_RecordsZeroTokens()
    {
        var trace = new LoopTraceCollector();
        var inner = new StubChat(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        var client = new TracingChatClient(inner, trace);

        await client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        trace.Build()[0].InputTokens.Should().Be(0);
        trace.Build()[0].OutputTokens.Should().Be(0);
    }

    [Fact]
    public async Task GetResponseAsync_ResponseWithoutModelId_RecordsUnknown()
    {
        var trace = new LoopTraceCollector();
        var inner = new StubChat(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok"))
        {
            Usage = new UsageDetails { InputTokenCount = 1, OutputTokenCount = 1 }
        });
        var client = new TracingChatClient(inner, trace);

        await client.GetResponseAsync(new[] { new ChatMessage(ChatRole.User, "hi") });

        trace.Build()[0].ModelName.Should().Be("unknown");
    }
}

using System.Text.RegularExpressions;
using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-10-07-6b9dc: the finaliser forwards only what fits 0.85 of the stated window; when
/// every tool result is at the floor it fails locally instead of sending a refused request.
/// </summary>
public sealed class ContextPressureFinalizingChatClientTests
{
    private const int Window = 100_000;

    private static readonly ChatOptions WithTools =
        new() { Tools = [AIFunctionFactory.Create(() => "x", "read_file")] };

    [Fact]
    public async Task Finaliser_RecentTailOutgrowsWindow_ForwardsUnderTheBound()
    {
        var inner = new CapturingChat();
        var client = new ContextPressureFinalizingChatClient(inner, Window, "DesignDialog");
        var messages = ForwardedViewFitTests.Loop(("a", ForwardedViewFitTests.Text(1_040_000)));

        await client.GetResponseAsync(messages, WithTools);

        CompactingChatClient.EstimateTokens(inner.Forwarded!).Should().BeLessThan((int)(Window * 0.85));
        inner.Forwarded!.Last().Text.Should().Be(ContextPressureFinalizingChatClient.FinalizeInstruction);
        ((string)messages.OfType<ChatMessage>().Last().Contents.OfType<FunctionResultContent>()
            .Single().Result!).Length.Should().Be(1_040_000);
    }

    [Fact]
    public async Task Finaliser_AllResultsAtFloor_ThrowsNamingNonToolShare()
    {
        var inner = new CapturingChat();
        var client = new ContextPressureFinalizingChatClient(inner, Window, "DesignDialog");
        var messages = ForwardedViewFitTests.Loop(("a", ForwardedViewFitTests.Text(200_000)));
        messages.Insert(0, new ChatMessage(ChatRole.System, ForwardedViewFitTests.Text(400_000)));

        var act = () => client.GetResponseAsync(messages, WithTools);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain("DesignDialog").And.Contain("100000");
        var stated = Regex.Match(thrown.Which.Message, @"~(?<estimate>\d+) tokens, (?<nonTool>\d+) of them not tool output");
        var estimate = int.Parse(stated.Groups["estimate"].Value);
        var nonTool = int.Parse(stated.Groups["nonTool"].Value);
        nonTool.Should().BeGreaterThanOrEqualTo(100_000, "the system prompt alone is 400,000 characters");
        (estimate - nonTool).Should().BeLessThanOrEqualTo(ForwardedViewFit.FloorChars / 4);
        inner.Forwarded.Should().BeNull();
    }

    [Fact]
    public async Task Finaliser_NothingToCutBelowWindow_ForwardsAsBefore()
    {
        var inner = new CapturingChat();
        var client = new ContextPressureFinalizingChatClient(inner, Window, "DesignDialog");
        // ~90,000 estimated tokens: above the 0.85 bound, below the window, no tool results.
        var messages = new List<ChatMessage> { new(ChatRole.System, ForwardedViewFitTests.Text(360_000)) };

        await client.GetResponseAsync(messages, WithTools);

        inner.Forwarded.Should().NotBeNull();
        inner.Forwarded!.Last().Text.Should().Be(ContextPressureFinalizingChatClient.FinalizeInstruction);
    }

    private sealed class CapturingChat : IChatClient
    {
        public List<ChatMessage>? Forwarded { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Forwarded = messages.ToList();
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "done")));
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}

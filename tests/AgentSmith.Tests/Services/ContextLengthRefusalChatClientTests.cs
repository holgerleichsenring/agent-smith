using AgentSmith.Infrastructure.Services.Providers.Agent;
using FluentAssertions;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.Services;

/// <summary>
/// 2026-08-27-3eb1: the provider's own words name neither the role nor a setting. Four
/// runs in a row died on "context_length_exceeded … 146231 &gt; 128000" and left nothing
/// to change.
/// </summary>
public sealed class ContextLengthRefusalChatClientTests
{
    private const string ProviderError =
        "HTTP 400 (invalid_request_error: context_length_exceeded): This model's maximum "
        + "context length is 128000 tokens, however you requested 146231 tokens.";

    [Fact]
    public async Task Refusal_ForContextLength_NamesTheRoleAndTheWindow()
    {
        var client = new ContextLengthRefusalChatClient(
            new ThrowingChat(ProviderError), "Scout", "gpt-4.1-mini", 128000);

        var act = () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], options: null, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<InvalidOperationException>();
        thrown.Which.Message.Should().Contain("Scout").And.Contain("gpt-4.1-mini")
            .And.Contain("128000").And.Contain("max_context_tokens");
        thrown.Which.InnerException!.Message.Should().Be(ProviderError);
    }

    [Fact]
    public async Task Refusal_WithNoStatedWindow_NamesTheSettingThatWouldHaveHelped()
    {
        var client = new ContextLengthRefusalChatClient(
            new ThrowingChat(ProviderError), "Scout", "gpt-4.1-mini", windowTokens: null);

        var act = () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], options: null, CancellationToken.None);

        (await act.Should().ThrowAsync<InvalidOperationException>())
            .Which.Message.Should().Contain("context_window_tokens");
    }

    [Fact]
    public async Task AnUnrelatedFailure_PassesThroughUntouched()
    {
        var client = new ContextLengthRefusalChatClient(
            new ThrowingChat("the response ended prematurely"), "Scout", "m", 128000);

        var act = () => client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "hi")], options: null, CancellationToken.None);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public void IsContextLengthRefusal_WrappedDeeper_IsStillRecognised() =>
        ContextLengthRefusalChatClient.IsContextLengthRefusal(
            new InvalidOperationException("call failed", new Exception("prompt is too long")))
            .Should().BeTrue();

    [Fact]
    public void Explain_NamesTheEstimateItMeasured() =>
        ContextLengthRefusalChatClient.Explain("Primary", "m", 200000, 210000)
            .Should().Contain("210000").And.Contain("200000");

    private sealed class ThrowingChat(string message) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new HttpRequestException(message);

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}

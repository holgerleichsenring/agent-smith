using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Infrastructure.Services.RateLimiting;
using FluentAssertions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging.Abstractions;

namespace AgentSmith.Tests.Services;

/// <summary>
/// p0374: the transient-network retry that keeps a single mid-stream connection
/// drop from failing a whole run (live cause: run …6fe6 killed at step 17 by
/// "The response ended prematurely" after 100+ good calls).
/// </summary>
public sealed class TransientRetryChatClientTests
{
    private static RetryConfig FastRetry(int max = 3) =>
        new() { MaxRetries = max, InitialDelayMs = 1, BackoffMultiplier = 1, MaxDelayMs = 1 };

    private static TransientRetryChatClient Wrap(IChatClient inner, RetryConfig retry) =>
        new(inner, retry, "test", NullLogger.Instance);

    [Fact]
    public async Task GetResponseAsync_TransientThenSuccess_RetriesAndReturns()
    {
        var inner = new ScriptedChatClient(
            new HttpRequestException("An error occurred while sending the request."),
            new IOException("The response ended prematurely."));
        var client = Wrap(inner, FastRetry());

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Text.Should().Be("ok");
        inner.Attempts.Should().Be(3, "2 transient failures then the success");
    }

    [Fact]
    public async Task GetResponseAsync_PersistentTransient_GivesUpAfterMaxRetriesAndThrows()
    {
        var inner = new ScriptedChatClient(
            new IOException("drop"), new IOException("drop"), new IOException("drop"), new IOException("drop"));
        var client = Wrap(inner, FastRetry(max: 2));

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        await act.Should().ThrowAsync<IOException>();
        inner.Attempts.Should().Be(3, "initial + MaxRetries(2) attempts, then it surfaces");
    }

    [Fact]
    public async Task GetResponseAsync_NonTransientError_NotRetried()
    {
        var inner = new ScriptedChatClient(new InvalidOperationException("bad request shape"));
        var client = Wrap(inner, FastRetry());

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        await act.Should().ThrowAsync<InvalidOperationException>();
        inner.Attempts.Should().Be(1, "a non-network error surfaces immediately (SDK owns 429/5xx)");
    }

    [Fact]
    public async Task GetResponseAsync_CancellationRequested_NotRetried()
    {
        var inner = new ScriptedChatClient(new HttpRequestException("drop"));
        var client = Wrap(inner, FastRetry());
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")], cancellationToken: cts.Token);

        await act.Should().ThrowAsync<Exception>();
        inner.Attempts.Should().Be(1, "a cancelled call is never retried");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void IsTransientNetwork_WalksInnerChain(bool wrapped)
    {
        Exception ex = new IOException("The response ended prematurely.");
        if (wrapped) ex = new InvalidOperationException("outer", ex);

        TransientRetryChatClient.IsTransientNetwork(ex).Should().BeTrue();
        TransientRetryChatClient.IsTransientNetwork(new InvalidOperationException("plain")).Should().BeFalse();
    }

    // A fake inner client that throws the scripted exceptions in order, then returns "ok".
    private sealed class ScriptedChatClient(params Exception[] failures) : IChatClient
    {
        public int Attempts { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            var i = Attempts++;
            if (i < failures.Length) throw failures[i];
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}

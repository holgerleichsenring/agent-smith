using System.ClientModel;
using System.ClientModel.Primitives;
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

    // p0477: the Azure and OpenAI SDKs report a status refusal as ClientResultException,
    // which the predicate could not see. A live run died on HTTP 429 sixty-two minutes in,
    // with twelve of fourteen ledger items done and both pull requests already open.
    [Fact]
    public async Task TransientRetry_ClientResultException429_IsRetried()
    {
        var inner = new ScriptedChatClient(RateLimited());
        var client = Wrap(inner, FastRetry());

        var response = await client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]);

        response.Text.Should().Be("ok");
        inner.Attempts.Should().Be(2, "a rate limit says the call arrived too soon, not that it is wrong");
    }

    [Fact]
    public async Task TransientRetry_ClientResultException503_IsRetried() =>
        TransientRetryChatClient.IsRetryableStatus(503).Should().BeTrue(
            "a server admitting its own fault is worth waiting for");

    [Fact]
    public async Task TransientRetry_ClientResultException400_IsNotRetried()
    {
        var inner = new ScriptedChatClient(new ClientResultException("bad request", new FakeResponse(400)));
        var client = Wrap(inner, FastRetry());

        await FluentActions.Awaiting(() => client.GetResponseAsync([new ChatMessage(ChatRole.User, "hi")]))
            .Should().ThrowAsync<ClientResultException>(
                "a 4xx that is not 408 or 429 can never succeed, and retrying burns time and money");
        inner.Attempts.Should().Be(1);
    }

    [Fact]
    public async Task TransientRetry_HttpRequestException429_StillRetried() =>
        TransientRetryChatClient.IsTransientNetwork(
            new HttpRequestException("too many", null, System.Net.HttpStatusCode.TooManyRequests))
            .Should().BeTrue("p0376's rule for this type is unchanged");

    private static ClientResultException RateLimited() =>
        new("rate_limit_exceeded", new FakeResponse(429));

    /// <summary>p0477: the minimum a ClientResultException needs to carry a status.</summary>
    private sealed class FakeResponse(int status) : PipelineResponse
    {
        public override int Status => status;
        public override string ReasonPhrase => "test";
        public override Stream? ContentStream { get => null; set { } }
        public override BinaryData Content => BinaryData.FromString(string.Empty);
        protected override PipelineResponseHeaders HeadersCore { get; } = new FakeHeaders();
        public override BinaryData BufferContent(CancellationToken ct = default) => Content;
        public override ValueTask<BinaryData> BufferContentAsync(CancellationToken ct = default) =>
            ValueTask.FromResult(Content);
        public override void Dispose() { }
    }

    private sealed class FakeHeaders : PipelineResponseHeaders
    {
        public override IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            Enumerable.Empty<KeyValuePair<string, string>>().GetEnumerator();
        public override bool TryGetValue(string name, out string? value) { value = null; return false; }
        public override bool TryGetValues(string name, out IEnumerable<string>? values) { values = null; return false; }
    }

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

    [Fact]
    public void IsTransientNetwork_Http400_NotTransient()
    {
        // p0376: a permanent 4xx (e.g. cache_control on a thinking block) must not be retried.
        var ex = new HttpRequestException("bad", null, System.Net.HttpStatusCode.BadRequest);
        TransientRetryChatClient.IsTransientNetwork(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransientNetwork_InvalidRequestBody_NoStatus_NotTransient()
    {
        // The SDK doesn't always set StatusCode; the invalid_request_error body still marks it permanent.
        var ex = new HttpRequestException(
            "{\"type\":\"error\",\"error\":{\"type\":\"invalid_request_error\",\"message\":\"messages.5...\"}}");
        TransientRetryChatClient.IsTransientNetwork(ex).Should().BeFalse();
    }

    [Fact]
    public void IsTransientNetwork_429AndGenuineFaults_StillTransient()
    {
        TransientRetryChatClient.IsTransientNetwork(
            new HttpRequestException("rate", null, System.Net.HttpStatusCode.TooManyRequests)).Should().BeTrue();
        TransientRetryChatClient.IsTransientNetwork(
            new HttpRequestException("connection reset")).Should().BeTrue();
        TransientRetryChatClient.IsTransientNetwork(
            new HttpRequestException("gateway", null, System.Net.HttpStatusCode.BadGateway)).Should().BeTrue();
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

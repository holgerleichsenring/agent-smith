using System.ClientModel;
using System.Runtime.CompilerServices;
using AgentSmith.Tests.Services;
using Microsoft.Extensions.AI;

namespace AgentSmith.Tests.ModelQuota;

/// <summary>2026-11-22-66e4a: a provider that fails its first calls as scripted, then answers with
/// <see cref="Usage"/>; it records each attempt.</summary>
internal sealed class QuotaScriptedClient(params Exception[] failures) : IChatClient
{
    private int _attempts;

    public int Attempts => Volatile.Read(ref _attempts);

    public UsageDetails? Usage { get; init; }

    /// <summary>Completes when the first attempt has been made.</summary>
    public TaskCompletionSource FirstAttempt { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        var i = Interlocked.Increment(ref _attempts) - 1;
        FirstAttempt.TrySetResult();
        if (i < failures.Length) throw failures[i];
        return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "ok")) { Usage = Usage });
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

    /// <summary>A throughput 429 naming its Retry-After in seconds.</summary>
    public static ClientResultException TooEarly(string retryAfterSeconds) =>
        new("rate_limit_exceeded", new FakeResponse(429, ("Retry-After", retryAfterSeconds))
        {
            Body = """{"error":{"code":"429","message":"exceeded token rate limit"}}""",
        });

    /// <summary>OpenAI's 429 for a spent credit.</summary>
    public static ClientResultException CreditSpent() =>
        new("insufficient_quota", new FakeResponse(429)
        {
            Body = """{"error":{"code":"insufficient_quota","message":"You exceeded your current quota"}}""",
        });
}

using System.Runtime.CompilerServices;
using Microsoft.Extensions.AI;

namespace AgentSmith.Infrastructure.Services.Factories.ChatClientBuilders;

/// <summary>
/// 2026-11-10-e706: bounds one model call by the agent's network timeout, for a provider library
/// whose client agent-smith cannot hand a timeout of its own (Ollama, Gemini). A call answers within
/// <see cref="Bound"/>; a streamed call delivers its next update within it, so a long answer that keeps
/// arriving is not cut. A call the bound ends fails with a <see cref="TimeoutException"/>; a caller's
/// own cancellation stays a cancellation.
/// </summary>
internal sealed class NetworkTimeoutChatClient(IChatClient inner, TimeSpan bound) : DelegatingChatClient(inner)
{
    internal TimeSpan Bound => bound;

    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadline.CancelAfter(bound);
        try
        {
            return await base.GetResponseAsync(messages, options, deadline.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Expired();
        }
    }

    public override async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
        IEnumerable<ChatMessage> messages, ChatOptions? options = null,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        using var quiet = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        await using var updates = base.GetStreamingResponseAsync(messages, options, quiet.Token)
            .GetAsyncEnumerator(quiet.Token);
        while (await NextAsync(updates, quiet, cancellationToken))
            yield return updates.Current;
    }

    // The bound runs only while the provider is awaited, never while the caller handles an update.
    private async Task<bool> NextAsync(
        IAsyncEnumerator<ChatResponseUpdate> updates, CancellationTokenSource quiet, CancellationToken cancellationToken)
    {
        quiet.CancelAfter(bound);
        try
        {
            var hasNext = await updates.MoveNextAsync();
            quiet.CancelAfter(Timeout.InfiniteTimeSpan);
            return hasNext;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw Expired();
        }
    }

    private TimeoutException Expired() => new(
        $"The model call sent nothing for {bound.TotalSeconds:F0}s (the agent's network timeout).");
}

using AgentSmith.Contracts.Models.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0374: retries a single LLM call on a TRANSIENT network failure — a mid-stream
/// connection drop (<c>HttpIOException "The response ended prematurely"</c>) or a
/// send-side <see cref="HttpRequestException"/> — instead of letting it fail the
/// whole run. The Anthropic SDK retries error STATUS codes (429 / 5xx) but not a
/// connection that dies mid-body; a live master-loop run (2026-07-24 …6fe6) was
/// killed at step 17 by exactly that after 100+ successful calls.
///
/// Bounded by <see cref="RetryConfig"/> (attempts + exponential backoff). Wraps
/// the rate-limited client, so every attempt re-acquires throttle capacity rather
/// than hammering. Cancellation is never retried. Non-streaming only:
/// GetResponseAsync re-sends the same materialised messages idempotently, while a
/// streaming response passes through untouched (replaying a partially-yielded
/// stream would duplicate output).
/// </summary>
internal sealed class TransientRetryChatClient(
    IChatClient inner, RetryConfig retry, string label, ILogger logger) : DelegatingChatClient(inner)
{
    public override async Task<ChatResponse> GetResponseAsync(
        IEnumerable<ChatMessage> messages,
        ChatOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var materialised = messages as IReadOnlyCollection<ChatMessage> ?? messages.ToList();
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await base.GetResponseAsync(materialised, options, cancellationToken);
            }
            catch (Exception ex) when (
                attempt < retry.MaxRetries
                && !cancellationToken.IsCancellationRequested
                && IsTransientNetwork(ex))
            {
                var delay = BackoffDelay(retry, attempt);
                logger.LogWarning(ex,
                    "Transient LLM network error for {Label} (attempt {Attempt}/{Max}); retrying in {Delay}ms",
                    label, attempt + 1, retry.MaxRetries, (int)delay.TotalMilliseconds);
                await Task.Delay(delay, cancellationToken);
            }
        }
    }

    // A connection-level failure — a send error or a response body that ended
    // prematurely. HttpIOException derives from IOException; HttpRequestException
    // covers the send side. A response STATUS error surfaces as a different type
    // and is left to the SDK's own 429/5xx retry. Walks the inner-exception chain
    // because the SDK often wraps the socket fault.
    internal static bool IsTransientNetwork(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
            if (e is HttpRequestException or IOException)
                return true;
        return false;
    }

    private static TimeSpan BackoffDelay(RetryConfig retry, int attempt)
    {
        var ms = retry.InitialDelayMs * Math.Pow(retry.BackoffMultiplier, attempt);
        return TimeSpan.FromMilliseconds(Math.Min(ms, retry.MaxDelayMs));
    }
}

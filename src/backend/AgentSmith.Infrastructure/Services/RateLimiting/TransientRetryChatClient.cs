using System.ClientModel;
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
        {
            if (e is HttpRequestException http)
            {
                // p0376: a 4xx is a PERMANENT client error — retrying it just burns time
                // and money (live: a 400 "invalid_request_error" from cache_control on a
                // thinking block was retried 5x, none could ever succeed). Only 408/429 are
                // worth a retry among 4xx. StatusCode isn't always populated by the SDK, so
                // also treat a body carrying invalid_request_error as permanent.
                if (http.StatusCode is { } status && (int)status is >= 400 and < 500
                    && status is not System.Net.HttpStatusCode.RequestTimeout
                    and not System.Net.HttpStatusCode.TooManyRequests)
                    return false;
                if (http.Message.Contains("invalid_request_error", StringComparison.Ordinal))
                    return false;
                return true;
            }
            if (e is IOException) return true;
            // p0477: the Azure and OpenAI SDKs report a status refusal as
            // ClientResultException, not HttpRequestException, so the walk above never saw
            // one — and the comment's assumption that "a status error is left to the SDK's
            // own 429/5xx retry" holds for Anthropic and not for these. A live run died on
            // HTTP 429 sixty-two minutes in with both pull requests already open. The rule
            // is the same whichever type carries the status: a 429 says the call arrived too
            // soon and will pass later, and every other 4xx says it can never pass.
            if (e is ClientResultException client) return IsRetryableStatus(client.Status);
        }
        return false;
    }

    /// <summary>A status worth waiting for: too-many-requests, request-timeout, or anything
    /// the server admits is its own.</summary>
    internal static bool IsRetryableStatus(int status) =>
        status is 408 or 429 || status >= 500;

    private static TimeSpan BackoffDelay(RetryConfig retry, int attempt)
    {
        var ms = retry.InitialDelayMs * Math.Pow(retry.BackoffMultiplier, attempt);
        return TimeSpan.FromMilliseconds(Math.Min(ms, retry.MaxDelayMs));
    }
}

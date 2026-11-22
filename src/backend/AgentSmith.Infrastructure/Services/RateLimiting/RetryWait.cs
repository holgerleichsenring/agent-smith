using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Net;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0493: how long a retried provider call waits, and why. A dropped connection is worth retrying in seconds; an
/// Azure throughput window is sixty seconds wide, so a rate limit waits the server's OWN Retry-After when one is sent,
/// and its own ladder when none is. One wait is bounded: a Retry-After above <see cref="Ceiling"/> is honoured up to
/// it, the clamp is stated, and the server is asked again after it.
/// <para>
/// 2026-11-22-66e4a: a throughput refusal is waited out by RateLimitingChatClient with no attempt cap — the
/// operator's decision; cancellation and the run watchdog end it — so the ceiling bounds one wait, not the call.
/// </para>
/// </summary>
internal readonly record struct RetryWait(TimeSpan Delay, string Reason)
{
    /// <summary>Twice Azure's sixty-second refill window: long enough for a server that is
    /// queueing rather than refusing, short enough that the server is asked again within two
    /// minutes.</summary>
    internal static readonly TimeSpan Ceiling = TimeSpan.FromSeconds(120);

    /// <summary>What a 429 that sent no header is worth waiting at minimum — a quarter of the
    /// refill window, where the network ladder would start at four seconds.</summary>
    internal static readonly TimeSpan RateLimitFloor = TimeSpan.FromSeconds(15);

    internal static RetryWait For(RetryConfig retry, int attempt, Exception ex)
    {
        ArgumentNullException.ThrowIfNull(retry);
        if (!IsRateLimit(ex)) return new(Backoff(retry, attempt), "transient network fault");
        if (RetryAfter(ex) is { } asked && asked > TimeSpan.Zero)
        {
            return asked <= Ceiling
                ? new(asked, "rate limit; waiting the interval the server asked for")
                : new(Ceiling, FormattableString.Invariant(
                    $"rate limit; the server asked for {asked.TotalSeconds:0}s, capped at {Ceiling.TotalSeconds:0}s"));
        }

        var ladder = Backoff(retry, attempt);
        return new(Clamp(ladder < RateLimitFloor ? RateLimitFloor : ladder),
            "rate limit; no Retry-After was sent, so the rate-limit ladder applies");
    }

    /// <summary>Whether the provider refused for THROUGHPUT — the one status that says the
    /// call was right and merely early. Both SDK families are read: Anthropic reports a
    /// status as <see cref="HttpRequestException"/>, Azure and OpenAI as
    /// <see cref="ClientResultException"/>.</summary>
    internal static bool IsRateLimit(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is ClientResultException client) return client.Status == 429;
            if (e is HttpRequestException http) return http.StatusCode == HttpStatusCode.TooManyRequests;
        }

        return false;
    }

    /// <summary>The interval the server itself named, when the refusal carries one: the response
    /// headers of a <see cref="ClientResultException"/>, or the rate limits the Anthropic SDK
    /// read into its <see cref="Anthropic.SDK.RateLimitsExceeded"/> (2026-11-22-66e4a).</summary>
    internal static TimeSpan? RetryAfter(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is ClientResultException client && client.GetRawResponse() is { } response)
                return FromHeaders(response.Headers);
            if (e is Anthropic.SDK.RateLimitsExceeded anthropic) return AnthropicRetryAfter(anthropic);
        }

        return null;
    }

    private static TimeSpan? AnthropicRetryAfter(Anthropic.SDK.RateLimitsExceeded refusal) =>
        refusal.RateLimits?.RetryAfter;

    /// <summary>Both spellings a provider uses: OpenAI answers in milliseconds, Azure in
    /// seconds or as an absolute HTTP date.</summary>
    private static TimeSpan? FromHeaders(PipelineResponseHeaders headers)
    {
        if (headers.TryGetValue("retry-after-ms", out var ms)
            && double.TryParse(ms, NumberStyles.Float, CultureInfo.InvariantCulture, out var milliseconds))
            return TimeSpan.FromMilliseconds(milliseconds);
        if (!headers.TryGetValue("Retry-After", out var value) || value is null) return null;
        if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var seconds))
            return TimeSpan.FromSeconds(seconds);
        return DateTimeOffset.TryParse(value, CultureInfo.InvariantCulture,
            DateTimeStyles.AdjustToUniversal, out var when)
            ? when - DateTimeOffset.UtcNow
            : null;
    }

    /// <summary>The operator's configured ladder, unchanged: this is what a network fault has
    /// waited since p0374.</summary>
    private static TimeSpan Backoff(RetryConfig retry, int attempt)
    {
        var ms = retry.InitialDelayMs * Math.Pow(retry.BackoffMultiplier, attempt);
        return TimeSpan.FromMilliseconds(Math.Min(ms, retry.MaxDelayMs));
    }

    private static TimeSpan Clamp(TimeSpan delay) => delay > Ceiling ? Ceiling : delay;
}

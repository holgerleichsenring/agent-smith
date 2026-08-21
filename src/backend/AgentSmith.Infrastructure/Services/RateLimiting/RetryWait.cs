using System.ClientModel;
using System.ClientModel.Primitives;
using System.Globalization;
using System.Net;
using AgentSmith.Contracts.Models.Configuration;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// p0493: how long a retried provider call waits, and why it waited that long.
/// <para>
/// A mid-stream socket fault and a rate limit are not the same kind of waiting. A dropped
/// connection is worth retrying in seconds; an Azure throughput window is SIXTY seconds
/// wide, so the configured network ladder (4s, 8s, 16s, 32s, 60s — about two minutes) is an
/// order of magnitude too impatient for a 429 and spends five attempts learning nothing.
/// </para>
/// <para>
/// So a rate limit waits the server's OWN Retry-After when one is sent, and its own ladder
/// when none is. Bounded either way: a Retry-After above <see cref="Ceiling"/> is honoured up
/// to it and the clamp is stated rather than swallowed, because a provider that asks for an
/// hour is naming a quota no run can wait out, and parking on it turns a legible failure into
/// a silent hang.
/// </para>
/// </summary>
internal readonly record struct RetryWait(TimeSpan Delay, string Reason)
{
    /// <summary>Twice Azure's sixty-second refill window: long enough for a server that is
    /// queueing rather than refusing, short enough that five waits at the ceiling stay well
    /// inside the 1800-second default run watchdog.</summary>
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

    /// <summary>The interval the server itself named, when the refusal carries one. Only
    /// <see cref="ClientResultException"/> reaches its response headers; the exception the
    /// Anthropic path throws carries no response to read.</summary>
    internal static TimeSpan? RetryAfter(Exception ex)
    {
        for (Exception? e = ex; e is not null; e = e.InnerException)
        {
            if (e is ClientResultException client && client.GetRawResponse() is { } response)
                return FromHeaders(response.Headers);
        }

        return null;
    }

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

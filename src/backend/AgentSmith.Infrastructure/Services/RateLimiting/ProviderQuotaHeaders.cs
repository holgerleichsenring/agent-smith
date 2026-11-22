using System.ClientModel.Primitives;
using System.Globalization;
using System.Runtime.CompilerServices;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: carries the provider's remaining-quota headers from the SDK pipeline up to the rate limiter. The
/// headers never reach the chat client — Microsoft.Extensions.AI keeps the completion, not the raw response — so
/// the limiter plants a box per call (the <see cref="ThrottleWaitReporter"/> pattern: AsyncLocal flows down, not
/// up) and <see cref="ProviderQuotaHeaderPolicy"/> fills it from every response it sees, success and refusal.
/// </summary>
public sealed class ProviderQuotaHeaders
{
    private readonly AsyncLocal<StrongBox<ProviderQuotaReading?>?> _current = new();

    /// <summary>Opens the box for one call; dispose restores the caller's.</summary>
    public Scope Begin()
    {
        var previous = _current.Value;
        var box = new StrongBox<ProviderQuotaReading?>();
        _current.Value = box;
        return new Scope(this, box, previous);
    }

    /// <summary>Called by the pipeline policy with each response's headers; a response without them leaves the box.</summary>
    public void Record(PipelineResponseHeaders headers)
    {
        if (_current.Value is not { } box) return;
        var reading = new ProviderQuotaReading(
            Number(headers, "x-ratelimit-remaining-tokens"), Number(headers, "x-ratelimit-remaining-requests"));
        if (reading.RemainingTokens is not null || reading.RemainingRequests is not null) box.Value = reading;
    }

    private static long? Number(PipelineResponseHeaders headers, string name) =>
        headers.TryGetValue(name, out var value)
        && long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number)
            ? number : null;

    public readonly struct Scope(
        ProviderQuotaHeaders owner, StrongBox<ProviderQuotaReading?> box, StrongBox<ProviderQuotaReading?>? previous)
        : IDisposable
    {
        /// <summary>The newest reading of this call, null when no response carried one.</summary>
        public ProviderQuotaReading? Reading => box.Value;

        /// <summary>Forgets the reading before a resend, so a refusal's reading never stands for the answer's.</summary>
        public void Clear() => box.Value = null;

        public void Dispose() => owner._current.Value = previous;
    }
}

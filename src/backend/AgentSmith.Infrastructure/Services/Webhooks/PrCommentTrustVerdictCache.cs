using System.Collections.Concurrent;

namespace AgentSmith.Infrastructure.Services.Webhooks;

/// <summary>
/// In-process verdict cache, five minutes per (host, repository, author) key. A permission
/// the host revokes therefore stops being honoured within five minutes at the latest.
/// </summary>
public sealed class PrCommentTrustVerdictCache(TimeProvider clock) : IPrCommentTrustVerdictCache
{
    private static readonly TimeSpan Lifetime = TimeSpan.FromMinutes(5);

    private readonly ConcurrentDictionary<string, Verdict> _verdicts = new(StringComparer.Ordinal);

    public async Task<bool> GetOrLookupAsync(string key, Func<Task<bool>> lookup)
    {
        var now = clock.GetUtcNow();
        if (_verdicts.TryGetValue(key, out var cached) && now - cached.At < Lifetime)
            return cached.Trusted;

        var trusted = await lookup();
        _verdicts[key] = new Verdict(trusted, now);
        return trusted;
    }

    private sealed record Verdict(bool Trusted, DateTimeOffset At);
}

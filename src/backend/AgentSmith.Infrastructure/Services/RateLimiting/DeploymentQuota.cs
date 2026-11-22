using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: what one deployment has left — its request and token buckets, a refusal's pause, the provider's
/// last reading and the learned ratio of counted to estimated input. Every member takes the one lock, so the
/// admission queue, a settling lease and the read-out see one state.
/// </summary>
internal sealed class DeploymentQuota
{
    private const double Resent = 1; // not a limit: a refused call is sent again as one more request

    private readonly object _lock = new();
    private readonly TimeProvider _clock;
    private readonly QuotaBucket _requests;
    private readonly QuotaBucket _tokens;
    private readonly InputCostRatio _ratio = new();
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;
    private ProviderQuotaReading? _reading;
    private DateTimeOffset? _readAt;
    private double _owed;
    private int _owedCalls;

    public DeploymentQuota(LlmRateLimitOptions options, TimeProvider clock)
    {
        if (options.RequestsPerMinute < 1 || options.InputTokensPerMinute < 1)
            throw new ConfigurationException(
                $"agent.rate_limit admits no call at {options.RequestsPerMinute} requests and {options.InputTokensPerMinute} "
                + "tokens per minute: requests_per_minute and input_tokens_per_minute must be at least 1.");
        _clock = clock;
        Rule = options.Rule ?? ProviderQuotaRule.EveryToken;
        _requests = new QuotaBucket(options.RequestsPerMinute, clock);
        _tokens = new QuotaBucket(options.InputTokensPerMinute, clock);
    }

    public ProviderQuotaRule Rule { get; }

    public QuotaReservation Reserve(LlmQuotaDemand demand)
    {
        lock (_lock) return QuotaReservation.For(demand, _ratio.Value, Rule, _tokens.Capacity);
    }

    /// <summary>How long until <paramref name="calls"/> requests and <paramref name="tokens"/> tokens fit, pause
    /// included; zero takes them at once.</summary>
    public TimeSpan TakeOrWait(double calls, double tokens, bool take)
    {
        lock (_lock)
        {
            var paused = _pausedUntil - _clock.GetUtcNow();
            var wait = Max(paused, Max(_requests.TimeUntil(calls), _tokens.TimeUntil(tokens)));
            if (take && wait <= TimeSpan.Zero) { _requests.Take(calls); _tokens.Take(tokens); }
            return wait;
        }
    }

    /// <summary>Debits <paramref name="delta"/> (negative gives back) and learns the counted-to-estimated ratio.</summary>
    public void Debit(double delta, double counted, double estimated)
    {
        lock (_lock) { _tokens.Take(delta); _ratio.Observe(counted, estimated); }
    }

    /// <summary>Caps both buckets at the provider's remaining quota plus what refilled since it was read (a call
    /// admitted here meanwhile is not in the reading: an accepted approximation, it only ever lowers the count).</summary>
    public void Align(ProviderQuotaReading reading, TimeSpan age)
    {
        lock (_lock)
        {
            if (reading.RemainingTokens is { } tokens) _tokens.CapAt(tokens + (_tokens.PerSecond * age.TotalSeconds));
            if (reading.RemainingRequests is { } calls) _requests.CapAt(calls + (_requests.PerSecond * age.TotalSeconds));
            (_reading, _readAt) = (reading, _clock.GetUtcNow());
        }
    }

    /// <summary>The provider refused a call of <paramref name="reserved"/> tokens: it has no room, every refused call
    /// not yet resent is owed once from the refill (a <paramref name="first"/> refusal joins them, giving back its
    /// admission), the resend is one more request, and every caller waits <paramref name="wait"/>.</summary>
    public void Refuse(int reserved, bool first, TimeSpan wait)
    {
        lock (_lock)
        {
            if (first) { _tokens.Take(-reserved); _owed += reserved; _owedCalls++; }
            _tokens.Take(-_owed);
            _tokens.CapAt(0);
            _tokens.Take(_owed);
            _requests.Take(Resent);
            var until = _clock.GetUtcNow() + wait;
            if (until > _pausedUntil) _pausedUntil = until;
        }
    }

    /// <summary>A refused call of <paramref name="reserved"/> tokens was resent and answered, or gave up.</summary>
    public void Release(int reserved)
    {
        lock (_lock) { _owed -= reserved; _owedCalls--; }
    }

    /// <summary>How long a refusal still pauses the deployment.</summary>
    public TimeSpan Paused()
    {
        lock (_lock) return Max(TimeSpan.Zero, _pausedUntil - _clock.GetUtcNow());
    }

    public LlmQuotaSnapshot Snapshot(ModelQuotaScope scope, int waiting)
    {
        lock (_lock)
        {
            var toFull = Max(_tokens.TimeUntil(_tokens.Capacity), _requests.TimeUntil(_requests.Capacity));
            return new LlmQuotaSnapshot(scope.Provider, scope.Deployment, _tokens.Capacity, _requests.Capacity,
                (long)Math.Max(0, _tokens.Level), (long)Math.Max(0, _requests.Level), toFull.TotalSeconds,
                Math.Max(0, (_pausedUntil - _clock.GetUtcNow()).TotalSeconds), waiting + _owedCalls,
                _reading?.RemainingTokens, _reading?.RemainingRequests, _readAt);
        }
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}

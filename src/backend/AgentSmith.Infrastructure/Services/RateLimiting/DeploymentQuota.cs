using AgentSmith.Contracts.Models.Configuration;
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
    private readonly QuotaRefusals _refusals = new();
    private ProviderQuotaReading? _reading;
    private DateTimeOffset? _readAt;

    public DeploymentQuota(LlmRateLimitOptions options, TimeProvider clock)
    {
        Validate(options);
        _clock = clock;
        Rule = options.Rule ?? ProviderQuotaRule.EveryToken;
        _requests = new QuotaBucket(options.RequestsPerMinute, clock);
        _tokens = new QuotaBucket(options.InputTokensPerMinute, clock);
    }

    public ProviderQuotaRule Rule { get; private set; }

    public int TokenCapacity { get { lock (_lock) return _tokens.Capacity; } }

    /// <summary>2026-11-22-66e4b: the budget changed in the configuration; what is free, owed and paused stays.</summary>
    public void Resize(LlmRateLimitOptions options)
    {
        Validate(options);
        lock (_lock)
        {
            _requests.Resize(options.RequestsPerMinute);
            _tokens.Resize(options.InputTokensPerMinute);
            Rule = options.Rule ?? ProviderQuotaRule.EveryToken;
        }
    }

    private static void Validate(LlmRateLimitOptions options)
    {
        if (AgentConfig.RateLimitProblem(options.RequestsPerMinute, options.InputTokensPerMinute) is { } problem)
            throw new ConfigurationException(problem);
    }

    public QuotaReservation Reserve(LlmQuotaDemand demand) { lock (_lock) return QuotaReservation.For(demand, _ratio.Value, Rule, _tokens.Capacity); }

    /// <summary>How long until <paramref name="calls"/> requests and <paramref name="tokens"/> tokens fit, pause
    /// included; zero takes them at once.</summary>
    public TimeSpan TakeOrWait(double calls, double tokens, bool take)
    {
        lock (_lock)
        {
            // 2026-11-22-66e4b: a demand reserved before the budget shrank waits for no more than the bucket holds, and
            // is still taken whole, so its lease settles against what was taken.
            var room = Max(_requests.TimeUntil(Math.Min(calls, _requests.Capacity)), _tokens.TimeUntil(Math.Min(tokens, _tokens.Capacity)));
            var wait = Max(_refusals.Remaining(_clock.GetUtcNow()), room);
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

    /// <summary>The provider refused a call (<see cref="QuotaRefusals.Refused"/>); the resend is one more request.</summary>
    public void Refuse(int reserved, bool first, TimeSpan wait)
    {
        lock (_lock)
        {
            _refusals.Refused(_tokens, reserved, first, _clock.GetUtcNow() + wait);
            _requests.Take(Resent);
        }
    }

    public void Release(int reserved) { lock (_lock) _refusals.Release(reserved); }

    /// <summary>How long a refusal still pauses the deployment.</summary>
    public TimeSpan Paused() { lock (_lock) return _refusals.Remaining(_clock.GetUtcNow()); }

    public LlmQuotaSnapshot Snapshot(ModelQuotaScope scope, int waiting)
    {
        lock (_lock)
        {
            var toFull = Max(_tokens.TimeUntil(_tokens.Capacity), _requests.TimeUntil(_requests.Capacity));
            return new LlmQuotaSnapshot(scope.Provider, scope.Deployment, _tokens.Capacity, _requests.Capacity,
                (long)Math.Max(0, _tokens.Level), (long)Math.Max(0, _requests.Level), toFull.TotalSeconds,
                _refusals.Remaining(_clock.GetUtcNow()).TotalSeconds, waiting + _refusals.OwedCalls,
                _reading?.RemainingTokens, _reading?.RemainingRequests, _readAt);
        }
    }

    private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;
}

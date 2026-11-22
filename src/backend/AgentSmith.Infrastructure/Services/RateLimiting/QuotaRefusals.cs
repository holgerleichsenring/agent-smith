namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: what provider refusals hold of one deployment — how long they pause every caller, and the
/// refused calls not yet resent, each owed once from the refill. Not thread-safe: <see cref="DeploymentQuota"/>
/// holds its lock around every use.
/// </summary>
internal sealed class QuotaRefusals
{
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;
    private double _owed;

    /// <summary>Refused calls waiting to be resent.</summary>
    public int OwedCalls { get; private set; }

    /// <summary>The provider refused a call of <paramref name="reserved"/> tokens: it has no room, every refused call
    /// not yet resent is owed once (a <paramref name="first"/> refusal joins them, giving back its admission), and
    /// every caller waits until <paramref name="until"/>.</summary>
    public void Refused(QuotaBucket tokens, int reserved, bool first, DateTimeOffset until)
    {
        if (first) { tokens.Take(-reserved); _owed += reserved; OwedCalls++; }
        tokens.Take(-_owed);
        tokens.CapAt(0);
        tokens.Take(_owed);
        if (until > _pausedUntil) _pausedUntil = until;
    }

    /// <summary>A refused call of <paramref name="reserved"/> tokens was resent and answered, or gave up.</summary>
    public void Release(int reserved)
    {
        _owed -= reserved;
        OwedCalls--;
    }

    /// <summary>How long the refusals still pause the deployment at <paramref name="now"/>.</summary>
    public TimeSpan Remaining(DateTimeOffset now) => _pausedUntil > now ? _pausedUntil - now : TimeSpan.Zero;
}

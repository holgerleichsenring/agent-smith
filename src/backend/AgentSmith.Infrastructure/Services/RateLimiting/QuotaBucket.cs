namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: one per-minute budget that refills continuously at capacity/60 per second, so a call that finds
/// it short waits only for the missing share (p0188's property). Unlike the TokenBucketRateLimiter it replaces, its
/// level can be settled after the fact and run into debt, capped at what the provider says is left, and asked how
/// long a demand will wait. Not thread-safe: <see cref="DeploymentQuota"/> holds its lock around every use.
/// </summary>
internal sealed class QuotaBucket
{
    private readonly TimeProvider _clock;
    private double _level;
    private long _stamp;

    public QuotaBucket(int capacity, TimeProvider clock)
    {
        Capacity = capacity; // 2026-11-22-66e4b: Resize sets it again when the budget changes
        // p0188 refilled at least one unit a second (TokensPerPeriod could not be 0); kept, so a five-request budget
        // does not slow to one request every twelve seconds.
        PerSecond = Math.Max(1.0, capacity / 60.0);
        _clock = clock;
        _level = capacity;
        _stamp = clock.GetTimestamp();
    }

    public int Capacity { get; private set; }

    public double PerSecond { get; private set; }

    /// <summary>2026-11-22-66e4b: a changed budget; what is free stays, never above the new size.</summary>
    public void Resize(int capacity)
    {
        Refill();
        Capacity = capacity;
        PerSecond = Math.Max(1.0, capacity / 60.0);
        _level = Math.Min(_level, capacity);
    }

    /// <summary>What is free now; negative is a debt the refill pays first.</summary>
    public double Level
    {
        get
        {
            Refill();
            return _level;
        }
    }

    /// <summary>Takes <paramref name="amount"/>; a negative amount gives back, never above capacity.</summary>
    public void Take(double amount)
    {
        Refill();
        _level = Math.Min(Capacity, _level - amount);
    }

    /// <summary>Never more free than <paramref name="ceiling"/>.</summary>
    public void CapAt(double ceiling)
    {
        Refill();
        _level = Math.Min(_level, ceiling);
    }

    /// <summary>How long until <paramref name="amount"/> is free; zero when it is.</summary>
    public TimeSpan TimeUntil(double amount)
    {
        Refill();
        return amount <= _level ? TimeSpan.Zero : TimeSpan.FromSeconds((amount - _level) / PerSecond);
    }

    private void Refill()
    {
        var now = _clock.GetTimestamp();
        var elapsed = _clock.GetElapsedTime(_stamp, now).TotalSeconds;
        _stamp = now;
        _level = Math.Min(Capacity, _level + (elapsed * PerSecond));
    }
}

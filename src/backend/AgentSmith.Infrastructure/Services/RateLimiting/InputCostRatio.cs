namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: how many tokens the provider counted per estimated input token, learned over the deployment's
/// recent calls. It replaces p0374's cache-share average: one ratio carries both the estimate's error (chars/4 read
/// 18k where Azure counted 50k) and the cache discount where a provider gives one (an Anthropic loop that is 99%
/// cache reads counts a small fraction). Weighted by size — counted and estimated tokens are averaged apart and
/// divided — so a small call whose estimate missed by a factor cannot swing what a large call reserves. Starts at 1,
/// the estimate as it stands. Not thread-safe: the <see cref="DeploymentQuota"/> lock covers it.
/// </summary>
internal sealed class InputCostRatio
{
    private const double Alpha = 0.4; // not a limit: the average's responsiveness, adapting in two to three calls

    private double _counted;
    private double _estimated;

    public double Value => _estimated > 0 ? _counted / _estimated : 1.0;

    /// <summary>Blends one call's counted and estimated input in; the first call seeds both.</summary>
    public void Observe(double counted, double estimated)
    {
        if (!(counted > 0) || !(estimated > 0)) return;
        var seeded = _estimated > 0;
        _counted = seeded ? (Alpha * counted) + ((1 - Alpha) * _counted) : counted;
        _estimated = seeded ? (Alpha * estimated) + ((1 - Alpha) * _estimated) : estimated;
    }
}

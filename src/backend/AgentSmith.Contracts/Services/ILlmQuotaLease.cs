using Microsoft.Extensions.AI;

namespace AgentSmith.Contracts.Services;

/// <summary>
/// 2026-11-22-66e4a: one call's hold on its deployment's budget, from admission to the provider's answer. The
/// defaults do nothing, so a limiter that meters nothing needs no lease logic.
/// </summary>
public interface ILlmQuotaLease
{
    /// <summary>The provider answered: debit what it counted (<paramref name="usage"/>) instead of the estimate,
    /// and cap the budget at the provider's own remaining quota (<paramref name="reading"/>), read
    /// <paramref name="callDuration"/> after the call was sent.</summary>
    void Settle(UsageDetails? usage, ProviderQuotaReading? reading, TimeSpan callDuration) { }

    /// <summary>The provider refused this call for throughput: nothing is left now, this call is owed from the
    /// refill, and every caller of the deployment waits <paramref name="wait"/>. Returns when the deployment is no
    /// longer paused; the call is then resent on this lease.</summary>
    Task RefusedAsync(TimeSpan wait, CancellationToken cancellationToken) => Task.Delay(wait, cancellationToken);

    /// <summary>2026-11-22-ab45: set by <see cref="Settle"/> when what the provider counted is more than the
    /// deployment's whole minute.</summary>
    QuotaShortfall? Shortfall => null;

    /// <summary>The call is over: a refused call no longer waits to be resent. Settling releases too; releasing twice
    /// does nothing.</summary>
    void Release() { }
}

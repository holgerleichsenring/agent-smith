using System.Diagnostics;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.RateLimiting;

/// <summary>
/// 2026-11-22-66e4a: waits out one throughput refusal of a call to <paramref name="label"/>. The lease pauses every
/// caller of the deployment for the interval <see cref="RetryWait"/> reads from the server and returns when no
/// refusal pauses it any more; the wait is logged with its reason, shown on the run and reported to the call's time
/// split. Which refusals qualify is <see cref="ProviderRefusalReader"/>'s answer.
/// </summary>
internal sealed class ThroughputRefusalWait(
    RetryConfig retry, string label, ThrottleWaitReporter waitReporter, ModelQuotaWaitLine? waitLine, ILogger logger)
{
    private readonly ProviderRefusalReader _reader = new(logger);

    /// <summary>Whether <paramref name="ex"/> is a refusal the call can outwait.</summary>
    public bool Applies(Exception ex) => _reader.IsThroughputRefusal(ex);

    public async Task OutwaitAsync(ILlmQuotaLease lease, Exception refusal, int refusals, CancellationToken cancellationToken)
    {
        var wait = RetryWait.For(retry, refusals, refusal);
        logger.LogWarning(refusal,
            "Model quota refused {Label} (refusal {Refusal}); every call to it waits, this one {Delay}ms — {Reason}",
            label, refusals + 1, (int)wait.Delay.TotalMilliseconds, wait.Reason);
        var waited = Stopwatch.StartNew();
        var outwait = lease.RefusedAsync(wait.Delay, cancellationToken); // the pause holds every caller at once
        try
        {
            if (waitLine is not null)
                await waitLine.WaitingAsync(label, wait.Delay, $"the provider refused the call ({wait.Reason})", cancellationToken);
            await outwait;
        }
        finally
        {
            waitReporter.Report(waited.ElapsedMilliseconds);
            if (waitLine is not null) await waitLine.OverAsync(label, CancellationToken.None);
        }
    }
}

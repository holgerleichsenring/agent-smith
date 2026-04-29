using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Runs `work(ct)` only while this replica holds the named lease. Renews the lease
/// every RenewInterval; loses it on renewal failure and falls back to idle-reacquire.
/// Parameterised by lease key so one instance handles pollers, another handles
/// housekeeping (shared infra, two independent leaders).
/// </summary>
public sealed class LeaderElectedHostedService(
    string leaseKey,
    Func<CancellationToken, Task> work,
    IRedisLeaderLease lease,
    ILogger<LeaderElectedHostedService> logger)
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Renew = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan IdleBackoff = TimeSpan.FromSeconds(5);

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Leader-elected service starting for {Key}", leaseKey);
        while (!cancellationToken.IsCancellationRequested)
        {
            var token = await lease.TryAcquireAsync(leaseKey, Ttl, cancellationToken);
            if (token is null)
            {
                try { await Task.Delay(IdleBackoff, cancellationToken); }
                catch (OperationCanceledException) { return; }
                continue;
            }

            await LeadAsync(token, cancellationToken);
        }
    }

    private async Task LeadAsync(string token, CancellationToken ct)
    {
        logger.LogInformation("Lease '{Key}' active — starting work delegate", leaseKey);
        using var inner = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var workTask = Task.Run(() => work(inner.Token), inner.Token);
        try
        {
            while (!ct.IsCancellationRequested)
            {
                try { await Task.Delay(Renew, ct); }
                catch (OperationCanceledException) { break; }

                if (!await lease.RenewAsync(leaseKey, token, Ttl, ct))
                {
                    logger.LogWarning("Lost lease {Key}, stopping work", leaseKey);
                    inner.Cancel();
                    break;
                }
            }
        }
        finally
        {
            inner.Cancel();
            try { await workTask; }
            catch (OperationCanceledException) { /* expected on shutdown / lease loss */ }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Leader-elected work for '{Key}' ended with an unhandled exception",
                    leaseKey);
            }
            await lease.ReleaseAsync(leaseKey, token, CancellationToken.None);
        }
    }
}

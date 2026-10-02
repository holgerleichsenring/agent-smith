using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Lifecycle;

/// <summary>
/// 2026-10-02-5f89e: renews a running run's liveness for its whole life — the run row's
/// beat for EVERY run, and the ActiveRun lease's when the run has a ticket. Extracted from
/// ExecutePipelineUseCase, which pumped the lease alone and so left a ticketless run with
/// no liveness signal: an init orphaned by a restart stayed "running" until someone
/// cancelled it. The two records guard different things — the lease guards the CLAIM,
/// which exists before any row; the row guards the RUN — and one pump writes both on one
/// cadence, so they cannot drift apart.
/// <para>
/// p0376: a transient fault on ONE renewal must not kill the pump. A dead pump freezes the
/// beat and a reaper then ends a LIVE run; only a cancellation of the pump's own token
/// means the run is ending — anything else is logged and retried on the next tick.
/// </para>
/// </summary>
public sealed class RunHeartbeatPump(
    IActiveRunLease lease,
    IRunHeartbeat heartbeat,
    TimeProvider timeProvider,
    ILogger<RunHeartbeatPump> logger)
{
    /// <summary>Well under the reapers' 3-minute stale threshold, so a legit
    /// multi-minute step never looks crashed.</summary>
    public static readonly TimeSpan Interval = TimeSpan.FromSeconds(45);

    public async Task RunAsync(string project, TicketId? ticketId, string runId, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(Interval, timeProvider, ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            await BeatAsync(project, ticketId, runId);
        }
    }

    /// <summary>One renewal of both records, each independent of the other's failure.
    /// CancellationToken.None: a beat that started lands even while the run ends.</summary>
    public async Task BeatAsync(string project, TicketId? ticketId, string runId)
    {
        try { await heartbeat.RenewAsync(runId, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Run heartbeat renewal failed for run {RunId} — retrying next tick", runId); }
        if (ticketId is null) return;
        try { await lease.RenewHeartbeatAsync(project, ticketId, CancellationToken.None); }
        catch (Exception ex) { logger.LogWarning(ex, "Lease heartbeat renewal failed for run {RunId} — retrying next tick", runId); }
    }
}

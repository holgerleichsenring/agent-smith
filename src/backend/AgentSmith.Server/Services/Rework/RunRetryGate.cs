using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59n: what the runs say about a Retry, before the tracker is read. Only the ticket's LATEST
/// attempt is retried — a newer queued reservation counts, which the previous-attempt reader cannot see. A
/// hand-back parks waiting with no finish, so it is answered before "not finished". The end must be one
/// <see cref="InfrastructureEnd"/> holds for — the same rule the ticket's attempt marks read.
/// </summary>
public sealed class RunRetryGate(
    TicketRunsQuery runQuery, TicketAttempts attempts, InfrastructureEnd infrastructure, TicketLaunchEnds launchEnds)
{
    /// <summary>Null when the run may be retried unchanged.</summary>
    public async Task<RetryRefusalView?> CheckAsync(string project, string ticketId, string runId, CancellationToken ct)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        RetryRefusalView Refuse(string reason, string text) => new(reason, text, project, ticketId);
        var head = attempts.Head(code).Run;
        if (head.Id != runId) return Refuse(RetryRefusals.NotLatest, NotLatest(head.Id, head.Status));
        if (head.Status == RunStatuses.WaitingForInput) return Refuse(RetryRefusals.HandedBack, RetryRefusals.HandedBackText);
        if (head.FinishedAt is null)
            return Refuse(RetryRefusals.NotFinished, $"run {runId} has not finished — it is {head.Status}");
        var launch = (await launchEnds.ForAsync([runId], ct)).GetValueOrDefault(runId);
        return infrastructure.Holds(head, launch) ? null : Refuse(RetryRefusals.NotInfrastructure, RetryRefusals.NotInfrastructureText);
    }

    private static string NotLatest(string headId, string status) => status == RunStatuses.Queued
        ? $"attempt {headId} is queued to start on this ticket — only the latest attempt is retried"
        : $"attempt {headId} is the ticket's latest — only the latest attempt is retried";
}

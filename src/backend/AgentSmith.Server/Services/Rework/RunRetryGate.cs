using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-13-9a59n: what the runs say about a Retry, before the tracker is read. Only the ticket's LATEST
/// attempt is retried — a newer queued reservation is an attempt already starting, which the previous-attempt
/// reader cannot see. The run asked must also be the latest the rework worker reads, or the stored request
/// would hang on a run the worker never looks at. A hand-back parks waiting with no finish, so it is answered
/// before "not finished". The button's end must be one <see cref="InfrastructureEnd"/> holds for — the same rule
/// the ticket's attempt marks read; the conversation's end may be anything but a delivered success.
/// </summary>
public sealed class RunRetryGate(
    TicketRunsQuery runQuery, TicketAttempts attempts, IPreviousAttemptReader worker, InfrastructureEnd infrastructure,
    TicketLaunchEnds launchEnds)
{
    /// <summary>Null when the run may be retried.</summary>
    public async Task<RetryRefusalView?> CheckAsync(
        string project, string ticketId, string runId, RetryOrigin origin, CancellationToken ct)
    {
        var code = (await runQuery.ForTicketAsync(project, ticketId, ct))
            .Where(r => r.Run.Pipeline == PipelinePresets.CodeName).ToList();
        RetryRefusalView Refuse(string reason, string text) => new(reason, text, project, ticketId);
        var head = attempts.Head(code).Run;
        if (head.Id != runId && head.Status == RunStatuses.Queued)
            return Refuse(RetryRefusals.Starting, $"{RetryRefusals.StartingText} — attempt {head.Id} is queued");
        if (head.Id != runId) return Refuse(RetryRefusals.NotLatest, NotLatest(head.Id));
        if ((await worker.LatestAsync(project, ticketId, null, ct))?.RunId is var read && read != runId)
            return Refuse(RetryRefusals.NotLatest, $"attempt {read ?? "none"} is the latest the rework worker reads — "
                + "only the latest attempt is retried");
        if (head.Status == RunStatuses.WaitingForInput)
            return Refuse(RetryRefusals.HandedBack, origin == RetryOrigin.Conversation
                ? RetryRefusals.HandedBackConversationText : RetryRefusals.HandedBackText);
        if (head.FinishedAt is null)
            return Refuse(RetryRefusals.NotFinished, $"run {runId} has not finished — it is {head.Status}");
        if (origin == RetryOrigin.Conversation)
            return head.Status == RunStatuses.Success ? Refuse(RetryRefusals.Delivered, RetryRefusals.DeliveredText) : null;
        var launch = (await launchEnds.ForAsync([runId], ct)).GetValueOrDefault(runId);
        return infrastructure.Holds(head, launch) ? null : Refuse(RetryRefusals.NotInfrastructure, RetryRefusals.NotInfrastructureText);
    }

    private static string NotLatest(string headId) =>
        $"attempt {headId} is the ticket's latest — only the latest attempt is retried";
}

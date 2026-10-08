using AgentSmith.Application.Services.Rework;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-2123: a status-change delivery (a Jira status move, a GitHub or GitLab reopen) claims
/// the ticket only as the move back of a FINISHED code attempt: the trigger names statuses and holds
/// this one literally (never "every status"), the newest code attempt finished, the move is newer
/// than what that attempt read — so a redelivery is no move — and nobody but a person made it (not
/// the tracker's own token, e.g. Retry or the rework entry). Otherwise the delivery starts nothing
/// and says nothing: no first-run trigger, no zero-match comment. The lease keeps it to one run.
/// </summary>
public sealed class StatusBackGate(IPreviousAttemptReader attempts, TrackerIdentity identity, WebhookSpawnDispatcher dispatcher)
{
    public async Task<WebhookResult> DispatchAsync(
        AgentSmithConfig config, IReadOnlyList<ProjectMatch> matches, IncomingTicketEnvelope envelope,
        string status, DateTimeOffset? at, TrackerActor? actor, CancellationToken ct)
    {
        var kept = new List<ProjectMatch>();
        foreach (var match in matches)
            if (await OpensAsync(config.Projects[match.ProjectName], match, envelope.TicketId!, status, at, actor, ct))
                kept.Add(match);
        if (kept.Count == 0) return WebhookResult.NotHandled("not a person's move back of a finished ticket");
        await dispatcher.DispatchAsync(config, kept, envelope, status, null, ct);
        return WebhookResult.HandledNoRoute();
    }

    private async Task<bool> OpensAsync(
        ResolvedProject project, ProjectMatch match, string ticketId, string status, DateTimeOffset? at, TrackerActor? actor, CancellationToken ct)
    {
        var statuses = TriggerSelectionHelper.ByKind(project, match.Kind)?.TriggerStatuses ?? [];
        if (statuses.Count == 0 || !statuses.Contains(status, StringComparer.OrdinalIgnoreCase) || at is not { } when) return false;
        if (await attempts.LatestAsync(project.Name, ticketId, null, ct) is not { Finished: true } attempt || !attempt.Precedes(when)) return false;
        return actor is not null && !actor.Is(await identity.SelfAsync(project.Tracker, ct));
    }
}

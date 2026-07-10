using System.Text.Json;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// p0320c: builds the two shapes the spawn funnel emits — the ClaimRequest for
/// an admitted head ticket and the CapacityQueueCandidate for a deferred one.
/// The candidate serializes the SAME initial context/plan answers the claim
/// would carry, so the pump can re-claim later without a fresh envelope (the
/// JSON round-trip matches the Redis job queue's, keeping value semantics
/// identical to a normally enqueued PipelineRequest).
/// </summary>
internal static class SpawnRequestBuilder
{
    public static ClaimRequest BuildRequest(
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers,
        string? existingRunId)
        => new(
            Platform: envelope.Platform!,
            ProjectName: project.Name,
            TicketId: new TicketId(envelope.TicketId!),
            PipelineName: pipelineName,
            InitialContext: BuildInitialContext(matchedTrigger),
            PlanAnswers: planAnswers,
            ExistingRunId: existingRunId);

    public static CapacityQueueCandidate BuildCandidate(
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers,
        string candidateRunId,
        string reason)
        => new(
            Project: project.Name,
            TicketId: envelope.TicketId!,
            Pipeline: pipelineName,
            Platform: envelope.Platform!,
            CandidateRunId: candidateRunId,
            Reason: reason,
            Repos: project.Repos.Select(r => r.Name).ToList(),
            // Never null for a funnel entry — the pump launches only entries that
            // carry an envelope (null marks the projector's TOCTOU backstop ones).
            InitialContextJson: JsonSerializer.Serialize(
                BuildInitialContext(matchedTrigger) ?? new Dictionary<string, object>()),
            PlanAnswersJson: planAnswers is null ? null : JsonSerializer.Serialize(planAnswers));

    private static Dictionary<string, object>? BuildInitialContext(WebhookTriggerConfig trigger)
    {
        var ctx = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(trigger.DoneStatus))
            ctx[ContextKeys.DoneStatus] = trigger.DoneStatus;
        // p0261: seed failed_status so a FAILED run terminalizes the native ticket
        // status (PipelineErrorHandler reads this). Unset → fall back to done_status,
        // so the ticket still leaves the open set rather than staying New/Active.
        var failed = !string.IsNullOrEmpty(trigger.FailedStatus) ? trigger.FailedStatus : trigger.DoneStatus;
        if (!string.IsNullOrEmpty(failed))
            ctx[ContextKeys.FailedStatus] = failed;
        // p0318: seed needs_clarification_status so the clarification gate can park the
        // ticket there. NO fallback to done_status — unset means "park not configured",
        // and the gate then posts the questions + halts without moving the status.
        if (!string.IsNullOrEmpty(trigger.NeedsClarificationStatus))
            ctx[ContextKeys.NeedsClarificationStatus] = trigger.NeedsClarificationStatus;
        return ctx.Count > 0 ? ctx : null;
    }
}

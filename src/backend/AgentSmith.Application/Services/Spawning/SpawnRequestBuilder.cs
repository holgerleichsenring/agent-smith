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
/// <para>
/// 2026-09-17-0e79a: both shapes carry the APPROVED RECORD when the ticket has one, so a run
/// launched now and a run the pump launches later work from the same approved set. Stated
/// consequence: it travels through the queue row and the Redis job payload — tens of kilobytes
/// per spec.
/// </para>
/// </summary>
internal static class SpawnRequestBuilder
{
    public static ClaimRequest BuildRequest(
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers,
        string? existingRunId,
        string? approvedSetJson = null)
        => new(
            Platform: envelope.Platform!,
            ProjectName: project.Name,
            TicketId: new TicketId(envelope.TicketId!),
            PipelineName: pipelineName,
            InitialContext: BuildInitialContext(matchedTrigger, approvedSetJson, envelope),
            PlanAnswers: planAnswers,
            ExistingRunId: existingRunId);

    public static CapacityQueueCandidate BuildCandidate(
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers,
        string candidateRunId,
        string reason,
        string? approvedSetJson = null)
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
                BuildInitialContext(matchedTrigger, approvedSetJson, envelope)
                ?? new Dictionary<string, object>()),
            PlanAnswersJson: planAnswers is null ? null : JsonSerializer.Serialize(planAnswers));

    private static Dictionary<string, object>? BuildInitialContext(
        WebhookTriggerConfig trigger, string? approvedSetJson, IncomingTicketEnvelope envelope)
    {
        var ctx = new Dictionary<string, object>();
        // A person named this run in chat: it rides the queue row too, so the pump's claim
        // reads the same answer the immediate claim does.
        if (envelope.RequestedByName) ctx[ContextKeys.RequestedByName] = true;
        if (envelope.SeriesAutoStart) ctx[ContextKeys.SeriesAutoStart] = true; // 2026-10-13-8762v
        if (envelope.PreconditionRelease) ctx[ContextKeys.PreconditionRelease] = true; // 2026-11-16-f3fbc

        // 2026-09-17-0e79a: the set a person approved, whole, under one key.
        if (!string.IsNullOrEmpty(approvedSetJson))
            ctx[ContextKeys.ApprovedSpecSet] = approvedSetJson;
        // 2026-11-18-d6d2a / 550aa: failed_status and done_status are not seeded — the run reads both from the
        // project (TicketParkStatuses), the one place their precedence lives.
        // p0318: seed needs_clarification_status so the clarification gate can park the
        // ticket there. NO fallback to done_status — unset means "park not configured",
        // and the gate then posts the questions + halts without moving the status.
        if (!string.IsNullOrEmpty(trigger.NeedsClarificationStatus))
            ctx[ContextKeys.NeedsClarificationStatus] = trigger.NeedsClarificationStatus;
        // p0390: the verdict park for a not-implementable ticket — a status discovery never
        // claims, so only an explicit operator Retry restarts the work.
        if (!string.IsNullOrEmpty(trigger.NotImplementableStatus))
            ctx[ContextKeys.NotImplementableStatus] = trigger.NotImplementableStatus;
        return ctx.Count > 0 ? ctx : null;
    }
}

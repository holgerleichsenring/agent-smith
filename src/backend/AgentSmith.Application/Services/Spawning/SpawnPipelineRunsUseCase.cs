using AgentSmith.Application.Services.Sandbox;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Sandbox;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// Builds exactly one ClaimRequest per ticket and submits it through
/// ITicketClaimService.ClaimAsync. The matched trigger is passed in so DoneStatus
/// can flow into the request's InitialContext exactly as today's webhook
/// handlers populate it. The unified-run model: one ticket = one pipeline run
/// over all configured repos (no per-repo fan-out).
///
/// p0269a: before claiming, a capacity probe pre-flights the run's sandbox
/// footprint against the target's live capacity (k8s ResourceQuota / Docker
/// concurrent-sandbox cap). No room → return Queued WITHOUT claiming, so the
/// ticket stays in its trigger status and the next poll retries. This is what
/// makes tickets that can't fit together run sequentially.
/// </summary>
public sealed class SpawnPipelineRunsUseCase(
    ITicketClaimService claimService,
    ISandboxResourceResolver resourceResolver,
    ISandboxCapacityProbe capacityProbe,
    ILogger<SpawnPipelineRunsUseCase> logger) : ISpawnPipelineRunsUseCase
{
    public async Task<SpawnResult> ExecuteAsync(
        AgentSmithConfig config,
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        CancellationToken ct,
        Dictionary<string, string>? planAnswers = null)
    {
        if (string.IsNullOrEmpty(envelope.TicketId))
            throw new ArgumentException("envelope.TicketId is required for spawn.", nameof(envelope));
        if (string.IsNullOrEmpty(envelope.Platform))
            throw new ArgumentException("envelope.Platform is required for spawn.", nameof(envelope));
        if (project.Repos.Count == 0)
            throw new InvalidOperationException(
                $"Project '{project.Name}' has no repos; cannot spawn pipeline runs.");

        // p0269a: capacity pre-flight. The footprint is the project-level resolved
        // size (context.yaml resources are only known after checkout, inside the run)
        // — pessimistic but safe. Deferral does NOT claim: the ticket is untouched
        // and the next poll re-evaluates once capacity frees.
        var footprint = resourceResolver.Resolve(project);
        var capacity = await capacityProbe.HasCapacityAsync(footprint, ct);
        if (!capacity.Admitted)
        {
            logger.LogInformation(
                "Admission deferred for project={Project} pipeline={Pipeline} ticket={Ticket}: {Reason}",
                project.Name, pipelineName, envelope.TicketId, capacity.Reason);
            return new SpawnResult(new[] { ClaimResult.Queued(capacity.Reason ?? "waiting for sandbox capacity") });
        }

        var request = BuildRequest(project, pipelineName, envelope, matchedTrigger, planAnswers);
        var result = await claimService.ClaimAsync(request, config, ct);

        logger.LogInformation(
            "Spawn for project={Project} pipeline={Pipeline} ticket={Ticket} → outcome={Outcome}",
            project.Name, pipelineName, envelope.TicketId, result.Outcome);

        return new SpawnResult(new[] { result });
    }

    private static ClaimRequest BuildRequest(
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers)
        => new(
            Platform: envelope.Platform!,
            ProjectName: project.Name,
            TicketId: new TicketId(envelope.TicketId!),
            PipelineName: pipelineName,
            InitialContext: BuildInitialContext(matchedTrigger),
            PlanAnswers: planAnswers);

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

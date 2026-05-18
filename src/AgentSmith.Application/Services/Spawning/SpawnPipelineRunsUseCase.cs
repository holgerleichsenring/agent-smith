using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Spawning;

/// <summary>
/// Builds one ClaimRequest per RepoConnection on the resolved project and submits the
/// batch through ITicketClaimService.ClaimSpawnAsync. The matched trigger is passed in
/// so DoneStatus can flow into the per-request InitialContext exactly as today's webhook
/// handlers populate it.
/// </summary>
public sealed class SpawnPipelineRunsUseCase(
    ITicketClaimService claimService,
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

        var requests = BuildRequests(project, pipelineName, envelope, matchedTrigger, planAnswers);
        var results = await claimService.ClaimSpawnAsync(requests, config, ct);

        logger.LogInformation(
            "Spawn for project={Project} pipeline={Pipeline} ticket={Ticket} → {RepoCount} repo(s); outcomes=[{Outcomes}]",
            project.Name, pipelineName, envelope.TicketId, requests.Count,
            string.Join(",", results.Select(r => r.Outcome)));

        return new SpawnResult(results);
    }

    private static IReadOnlyList<ClaimRequest> BuildRequests(
        ResolvedProject project,
        string pipelineName,
        IncomingTicketEnvelope envelope,
        WebhookTriggerConfig matchedTrigger,
        Dictionary<string, string>? planAnswers)
    {
        var ticketId = new TicketId(envelope.TicketId!);
        var initialContext = BuildInitialContext(matchedTrigger);
        var requests = new ClaimRequest[project.Repos.Count];
        for (var i = 0; i < project.Repos.Count; i++)
            requests[i] = new ClaimRequest(
                Platform: envelope.Platform!,
                ProjectName: project.Name,
                TicketId: ticketId,
                PipelineName: pipelineName,
                InitialContext: initialContext,
                PlanAnswers: planAnswers,
                RepoName: project.Repos[i].Name);
        return requests;
    }

    private static Dictionary<string, object>? BuildInitialContext(WebhookTriggerConfig trigger)
    {
        if (string.IsNullOrEmpty(trigger.DoneStatus)) return null;
        return new Dictionary<string, object> { [ContextKeys.DoneStatus] = trigger.DoneStatus };
    }
}

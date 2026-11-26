using AgentSmith.Application.Services.Polling;
using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Rework;

/// <summary>
/// 2026-10-08-0781: a keyword comment the rework entry says is no rework still starts what a
/// comment starts — the route the poller would take, read from the ticket as it is NOW: envelope,
/// the nudge project's match, its trigger, the current status, then the spawn funnel. Never the
/// webhook dispatcher, which would hand the ticket back to this queue.
/// 2026-11-26-5f6e: answers what the spawn did, or why nothing routes the ticket now, so a start reports it.
/// </summary>
public sealed class ReworkFallbackSpawn(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ITicketProviderFactory tickets,
    PolledTicketEnvelope envelopes,
    IEnvelopeProjectResolver resolver,
    ISpawnPipelineRunsUseCase spawn,
    ILogger<ReworkFallbackSpawn> logger)
{
    public async Task<(SpawnResult? Spawned, string? Nowhere)> SpawnAsync(ResolvedProject project, string ticketId, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        var ticket = await tickets.Create(project.Tracker).GetTicketAsync(new TicketId(ticketId), ct);
        var envelope = await envelopes.ForAsync(project.Tracker, ticket, ct);
        var match = resolver.Resolve(config, envelope).Where(m => m.ProjectName == project.Name)
            .Select(m => (ProjectMatch?)m).FirstOrDefault();
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type);
        if (match is null || trigger is null || !Allowed(trigger, ticket.Status))
        {
            logger.LogInformation("Comment on {Project}/#{Ticket} routes nowhere now — status {Status}", project.Name, ticketId, ticket.Status);
            return (null, Nowhere(project, match is null, trigger, ticket.Status));
        }
        return (await spawn.ExecuteAsync(config, project, match.Value.PipelineName, envelope, trigger, ct), null);
    }

    private static string Nowhere(ResolvedProject project, bool unmatched, WebhookTriggerConfig? trigger, string status) =>
        trigger is null ? $"project '{project.Name}' declares no {project.Tracker.Type} trigger"
            : unmatched ? $"the ticket does not resolve to project '{project.Name}' by its labels and pipeline_from_label"
            : $"its status '{status}' is none of the trigger statuses ({string.Join(", ", trigger.TriggerStatuses)})";

    private static bool Allowed(WebhookTriggerConfig trigger, string status) =>
        trigger.TriggerStatuses.Count == 0 || trigger.TriggerStatuses.Contains(status, StringComparer.OrdinalIgnoreCase);
}

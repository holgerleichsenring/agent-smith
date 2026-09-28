using AgentSmith.Application.Services.Triggers;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Models.Triggers;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// Starts a ticket's run a person asked for in chat through the spawn funnel — capacity
/// admission, the FIFO queue and the approved-set carrier — exactly as a polled or webhook
/// ticket starts. The envelope speaks for the project's tracker (its platform, its trigger),
/// so the run's done/failed statuses resolve as a routed run's do; it is marked as named by a
/// person, so the claim skips only the question of whether a label would have routed it.
/// </summary>
public sealed class ChatTicketRunLauncher(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    ISpawnPipelineRunsUseCase spawnUseCase,
    ILogger<ChatTicketRunLauncher> logger)
{
    public async Task<ChatLaunchResult> LaunchAsync(
        string projectName, string ticketId, string pipeline, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project))
            return ChatLaunchResult.Refused($"Project '{projectName}' is not configured.");
        if (project.Repos.Count == 0)
            return ChatLaunchResult.Refused($"Project '{projectName}' has no repos to run on.");

        var envelope = new IncomingTicketEnvelope
        {
            TicketId = ticketId.TrimStart('#'),
            Platform = project.Tracker.Type.ToString().ToLowerInvariant(),
            RequestedByName = true,
        };
        var trigger = TriggerSelectionHelper.ByTrackerType(project, project.Tracker.Type)
            ?? new WebhookTriggerConfig();
        var spawn = await spawnUseCase.ExecuteAsync(config, project, pipeline, envelope, trigger, ct);
        logger.LogInformation(
            "Chat run for {Project} ticket {Ticket} pipeline {Pipeline} → run {RunId}",
            project.Name, envelope.TicketId, pipeline, spawn.RunId);
        return ToResult(spawn);
    }

    private static ChatLaunchResult ToResult(SpawnResult spawn)
    {
        var claim = spawn.ClaimResults.FirstOrDefault();
        return claim?.Outcome switch
        {
            ClaimOutcome.Claimed when spawn.RunId is not null => ChatLaunchResult.Started(spawn.RunId),
            ClaimOutcome.Queued when spawn.RunId is not null =>
                ChatLaunchResult.Waiting(spawn.RunId, claim.Error ?? "waiting for capacity"),
            ClaimOutcome.AlreadyClaimed => ChatLaunchResult.Refused("This ticket already has a run in flight."),
            ClaimOutcome.Rejected => ChatLaunchResult.Refused(claim.Error ?? $"Refused: {claim.Rejection}."),
            _ => ChatLaunchResult.Refused(claim?.Error ?? "The run could not be started."),
        };
    }
}

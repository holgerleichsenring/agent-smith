using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.Adapters;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the FixTicketIntent: validates preconditions, spawns an agent job,
/// and registers it in the conversation state for progress tracking.
/// </summary>
public sealed class FixTicketIntentHandler(
    IJobSpawner spawner,
    IPlatformAdapter adapter,
    ConversationStateManager stateManager,
    MessageBusListener listener,
    ILogger<FixTicketIntentHandler> logger)
{
    public async Task HandleAsync(FixTicketIntent intent, CancellationToken cancellationToken)
    {
        var existing = await stateManager.GetAsync(intent.Platform, intent.ChannelId, cancellationToken);
        if (existing is not null)
        {
            await SendAlreadyRunningAsync(intent, existing, cancellationToken);
            return;
        }

        await adapter.SendMessageAsync(
            intent.ChannelId,
            $"Starting Agent Smith for ticket *#{intent.TicketId}* in *{intent.Project}*...",
            cancellationToken);

        var request = new JobRequest
        {
            InputCommand = $"fix #{intent.TicketId} in {intent.Project}",
            Project = intent.Project,
            ChannelId = intent.ChannelId,
            UserId = intent.UserId,
            Platform = intent.Platform,
            PipelineOverride = intent.PipelineOverride
        };

        var jobId = await spawner.SpawnAsync(request, cancellationToken);
        await RegisterJobAsync(jobId, intent, cancellationToken);

        logger.LogInformation(
            "Job {JobId} spawned for ticket #{TicketId} in {Project} (channel={ChannelId})",
            jobId, intent.TicketId, intent.Project, intent.ChannelId);
    }

    private async Task SendAlreadyRunningAsync(
        FixTicketIntent intent,
        ConversationState existing,
        CancellationToken cancellationToken)
    {
        await adapter.SendMessageAsync(
            intent.ChannelId,
            $":hourglass: There is already a job running for this channel " +
            $"(job `{existing.JobId}` for ticket #{existing.TicketId}). " +
            "Please wait for it to complete.",
            cancellationToken);
    }

    private async Task RegisterJobAsync(
        string jobId,
        FixTicketIntent intent,
        CancellationToken cancellationToken)
    {
        var state = new ConversationState
        {
            JobId = jobId,
            ChannelId = intent.ChannelId,
            UserId = intent.UserId,
            Platform = intent.Platform,
            Project = intent.Project,
            TicketId = intent.TicketId,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow
        };

        await stateManager.SetAsync(state, cancellationToken);
        await stateManager.IndexJobAsync(state, cancellationToken);
        await listener.TrackJobAsync(jobId, cancellationToken);
    }
}

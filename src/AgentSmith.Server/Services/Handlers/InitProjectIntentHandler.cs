using AgentSmith.Dispatcher.Contracts;
using AgentSmith.Dispatcher.Models;
using AgentSmith.Dispatcher.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Dispatcher.Services.Handlers;

/// <summary>
/// Handles the InitProjectIntent: spawns an agent job that runs the
/// init-project pipeline (checkout → bootstrap → commit/PR).
/// </summary>
public sealed class InitProjectIntentHandler(
    IJobSpawner spawner,
    IPlatformAdapter adapter,
    ConversationStateManager stateManager,
    MessageBusListener listener,
    ILogger<InitProjectIntentHandler> logger)
{
    public async Task HandleAsync(InitProjectIntent intent, CancellationToken cancellationToken)
    {
        var existing = await stateManager.GetAsync(intent.Platform, intent.ChannelId, cancellationToken);
        if (existing is not null)
        {
            await adapter.SendMessageAsync(
                intent.ChannelId,
                $":hourglass: There is already a job running for this channel " +
                $"(job `{existing.JobId}`). Please wait for it to complete.",
                cancellationToken);
            return;
        }

        await adapter.SendMessageAsync(
            intent.ChannelId,
            $"Starting project initialization for *{intent.Project}*...",
            cancellationToken);

        var request = new JobRequest
        {
            InputCommand = $"init {intent.Project}",
            Project = intent.Project,
            ChannelId = intent.ChannelId,
            UserId = intent.UserId,
            Platform = intent.Platform,
            PipelineOverride = "init-project"
        };

        var jobId = await spawner.SpawnAsync(request, cancellationToken);

        var state = new ConversationState
        {
            JobId = jobId,
            ChannelId = intent.ChannelId,
            UserId = intent.UserId,
            Platform = intent.Platform,
            Project = intent.Project,
            TicketId = 0,
            StartedAt = DateTimeOffset.UtcNow,
            LastActivityAt = DateTimeOffset.UtcNow
        };

        await stateManager.SetAsync(state, cancellationToken);
        await stateManager.IndexJobAsync(state, cancellationToken);
        await listener.TrackJobAsync(jobId, cancellationToken);

        logger.LogInformation(
            "Job {JobId} spawned for init-project in {Project} (channel={ChannelId})",
            jobId, intent.Project, intent.ChannelId);
    }
}

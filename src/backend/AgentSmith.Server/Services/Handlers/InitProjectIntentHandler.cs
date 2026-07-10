using AgentSmith.Application.Services.Orchestrator;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Adapters;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Handlers;

/// <summary>
/// Handles the InitProjectIntent: spawns an agent job that runs the
/// init-project pipeline (checkout → bootstrap → commit/PR), with the
/// orchestrator image + resources resolved per-project.
/// </summary>
public sealed class InitProjectIntentHandler(
    IJobSpawner spawner,
    IPlatformAdapter adapter,
    ConversationStateManager stateManager,
    MessageBusListener listener,
    IOrchestratorImageResolver orchestratorImageResolver,
    IOrchestratorResourceResolver orchestratorResourceResolver,
    IConfigurationLoader configurationLoader,
    ServerContext serverContext,
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

        var projectConfig = ResolveProjectConfig(intent.Project);
        var request = BuildJobRequest(intent, projectConfig);
        var jobId = await spawner.SpawnAsync(request, cancellationToken);
        await RegisterJobAsync(jobId, intent, cancellationToken);

        logger.LogInformation(
            "Job {JobId} spawned for init-project in {Project} (channel={ChannelId}, image={Image})",
            jobId, intent.Project, intent.ChannelId, request.OrchestratorImage);
    }

    private ResolvedProject ResolveProjectConfig(string projectName)
    {
        var config = configurationLoader.LoadConfig(serverContext.ConfigPath);
        if (config.Projects.TryGetValue(projectName, out var found)) return found;
        throw new InvalidOperationException(
            $"Project '{projectName}' is not defined in agentsmith.yml.");
    }

    private JobRequest BuildJobRequest(InitProjectIntent intent, ResolvedProject projectConfig) => new()
    {
        InputCommand = $"init {intent.Project}",
        Project = intent.Project,
        ChannelId = intent.ChannelId,
        UserId = intent.UserId,
        Platform = intent.Platform,
        OrchestratorImage = orchestratorImageResolver.Resolve(projectConfig),
        // p0320b: the interface answers null only for in-process compositions; the
        // Server composition always registers the JobSpawnerOptions-backed resolver.
        OrchestratorResources = orchestratorResourceResolver.Resolve(projectConfig)
            ?? throw new InvalidOperationException(
                "No orchestrator resources resolved — the Server composition must register OrchestratorResourceResolver."),
        PipelineOverride = "init-project"
    };

    private async Task RegisterJobAsync(
        string jobId,
        InitProjectIntent intent,
        CancellationToken cancellationToken)
    {
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
    }
}

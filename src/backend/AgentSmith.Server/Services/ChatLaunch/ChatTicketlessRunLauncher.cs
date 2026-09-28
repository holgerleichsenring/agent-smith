using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Init;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.ChatLaunch;

/// <summary>
/// Starts a run with no ticket that a person asked for in chat — a security review of a
/// project or of one pull request. There is nothing to claim, so it takes the manual init's
/// shape: the run id minted here, admitted synchronously (a run that does not fit is refused
/// with the reason, never queued — the capacity queue re-validates a ticket and this has
/// none), the queued row written so the id is linkable at once, then enqueued for the queue
/// consumer, which runs it like any other run.
/// </summary>
public sealed class ChatTicketlessRunLauncher(
    InitRunRepository runs,
    InitRunAdmission admission,
    IRedisJobQueue jobQueue,
    TimeProvider timeProvider,
    ILogger<ChatTicketlessRunLauncher> logger)
{
    private const string QueuedSummary = "starting — requested from chat";

    public async Task<ChatLaunchResult> LaunchAsync(
        ResolvedProject project, string pipeline, Dictionary<string, object> context, CancellationToken ct)
    {
        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        var decision = await admission.TryAdmitAsync(project, pipeline, runId, ct);
        if (!decision.Admitted) return ChatLaunchResult.Refused(decision.Reason!);

        await runs.CreateQueuedRunAsync(runId, project.Name, pipeline, ReposOf(project, context), QueuedSummary, ct);
        await jobQueue.EnqueueAsync(new PipelineRequest(
            project.Name, pipeline, TicketId: null, Headless: true, Context: context, RunId: runId), ct);

        logger.LogInformation(
            "Chat launched {Pipeline} for project {Project} (run {RunId}) — ticketless, trigger manual",
            pipeline, project.Name, runId);
        return ChatLaunchResult.Started(runId);
    }

    // A run pinned to one repo (a pull request's) records that repo, not the whole project.
    private static IReadOnlyList<string> ReposOf(ResolvedProject project, Dictionary<string, object> context) =>
        context.TryGetValue(ContextKeys.SourceOverrideRepo, out var repo) && repo is string name
            ? [name]
            : project.Repos.Select(r => r.Name).ToList();
}

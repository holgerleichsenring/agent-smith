using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;

namespace AgentSmith.Server.Services.Init;

/// <summary>
/// p0489: starts the init-project pipeline for a configured project on the
/// operator's word. ResumeRunLauncher's shape — admit, record, enqueue — with no
/// claim and no tracker call: init-project is ticketless by design, so the request
/// carries no TicketId and no ticket is created, read or transitioned. The queue
/// consumer then runs it exactly like a polled run, which is where the ledger,
/// cancel, delete and cost accounting come from.
/// </summary>
public sealed class InitRunLauncher(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    InitRunRepository runs,
    InitRunAdmission admission,
    IRedisJobQueue jobQueue,
    TimeProvider timeProvider,
    ILogger<InitRunLauncher> logger)
{
    /// <summary>The only pipeline this launcher starts — init is the one thing an
    /// operator triggers by hand, so the surface takes no pipeline parameter.</summary>
    public const string PipelineName = "init-project";

    private const string QueuedSummary = "starting — initialization requested from the dashboard";

    /// <param name="autoCompletePullRequests">p0490: the operator's auto-accept, as
    /// ticked on THIS launch. It rides the enqueued request into the pipeline context,
    /// where the init pipeline's last step reads it.</param>
    public async Task<InitLaunchResult> LaunchAsync(
        string projectName, bool autoCompletePullRequests, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project))
            return InitLaunchResult.UnknownProject(projectName);

        var live = await runs.FindLiveRunIdAsync(projectName, PipelineName, ct);
        if (live is not null) return InitLaunchResult.AlreadyRunning(live);

        return await AdmitAndEnqueueAsync(project, autoCompletePullRequests, ct);
    }

    private async Task<InitLaunchResult> AdmitAndEnqueueAsync(
        ResolvedProject project, bool autoCompletePullRequests, CancellationToken ct)
    {
        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        var decision = await admission.TryAdmitAsync(project, PipelineName, runId, ct);
        if (!decision.Admitted) return InitLaunchResult.NoCapacity(decision.Reason!);

        // The pre-start row makes the answered run id valid the moment the operator
        // gets it, and is what the double-start guard reads. RunEventApplier promotes
        // it to running when the run's RunStarted lands.
        await runs.CreateQueuedRunAsync(
            runId, project.Name, PipelineName,
            project.Repos.Select(r => r.Name).ToList(), QueuedSummary, ct);
        await jobQueue.EnqueueAsync(
            ToRequest(project.Name, runId, autoCompletePullRequests), ct);

        logger.LogInformation(
            "Init launched for project {Project} (run {RunId}) — ticketless, trigger manual",
            project.Name, runId);
        return InitLaunchResult.Started(runId);
    }

    private static PipelineRequest ToRequest(
        string projectName, string runId, bool autoCompletePullRequests) => new(
        projectName, PipelineName, TicketId: null, IsInit: true, Headless: true, RunId: runId,
        Context: new Dictionary<string, object>
        {
            [ContextKeys.AutoCompletePullRequests] = autoCompletePullRequests,
        });
}

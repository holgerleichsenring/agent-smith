using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;

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
    QueuedRunDispatch dispatch,
    IRedisClaimLock launchLock,
    TimeProvider timeProvider,
    ILogger<InitRunLauncher> logger)
{
    /// <summary>The only pipeline this launcher starts — init is the one thing an
    /// operator triggers by hand, so the surface takes no pipeline parameter.</summary>
    public const string PipelineName = "init-project";

    private const string QueuedSummary = "starting — initialization requested";

    // 2026-10-02-5f89d: held across guard, admission and insert, which are separate awaits —
    // two launches could both read "no live run" and both insert. Long enough to outlast
    // the admission's probe; the release is a CAS, so an expired lock is never deleted.
    private static readonly TimeSpan LaunchLockTtl = TimeSpan.FromSeconds(30);

    /// <param name="autoCompletePullRequests">p0490: the operator's auto-accept, as
    /// ticked on THIS launch. It rides the enqueued request into the pipeline context,
    /// where the init pipeline's last step reads it.</param>
    public async Task<InitLaunchResult> LaunchAsync(
        string projectName, bool autoCompletePullRequests, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project))
            return InitLaunchResult.UnknownProject(projectName);

        var lockKey = $"agentsmith:init-launch:{project.Name}";
        var token = await launchLock.TryAcquireAsync(lockKey, LaunchLockTtl, ct);
        if (token is null) return InitLaunchResult.BeingStarted();
        try { return await GuardAndLaunchAsync(project, autoCompletePullRequests, ct); }
        finally { await launchLock.ReleaseAsync(lockKey, token, CancellationToken.None); }
    }

    private async Task<InitLaunchResult> GuardAndLaunchAsync(
        ResolvedProject project, bool autoCompletePullRequests, CancellationToken ct)
    {
        // p0515: the double-start guard reads the row the launch WRITES, and that row
        // carries project.Name — the configured spelling. Guarding on the route's spelling
        // made "Demo" and "demo" two independent runs of one project on SQLite/Postgres,
        // where the comparison happens in SQL.
        var live = await runs.FindLiveRunAsync(project.Name, PipelineName, ct);
        if (live is not null) return InitLaunchResult.AlreadyRunning(live.Id);

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
        // 2026-10-02-5ab2b: stored on the row, then pushed — a flush cannot take it.
        await dispatch.DispatchAsync(ToRequest(project.Name, runId, autoCompletePullRequests), ct);

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

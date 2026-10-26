using AgentSmith.Application.Models;
using AgentSmith.Application.Services;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Init;
using AgentSmith.Server.Services.Lifecycle;

namespace AgentSmith.Server.Services.Derivation;

/// <summary>
/// 2026-10-26-bdd0e: starts a spec derivation of one ticket on a person's word. InitRunLauncher's shape — admit,
/// record, dispatch — under the ticket's LEASE, taken here directly: no claim, so no taken-ticket record and no
/// tracker write. The lease keeps the poller, the rework worker and every claim off the ticket for the whole
/// derivation; the run attaches and releases it, and the reaper frees it after a crash.
/// <para>
/// Once the row is stored nothing is rolled back: the sweeper pushes it again and the run attaches the lease, or
/// the lease goes stale and is reaped.
/// </para>
/// </summary>
public sealed class SpecDerivationLauncher(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IActiveRunLease lease,
    SpecDerivationGate gate,
    InitRunAdmission admission,
    DerivationRunRepository runs,
    QueuedRunDispatch dispatch,
    IRedisClaimLock launchLock,
    TimeProvider timeProvider,
    ILogger<SpecDerivationLauncher> logger)
{
    private static readonly TimeSpan LaunchLockTtl = TimeSpan.FromSeconds(30);

    public async Task<SpecDerivationLaunch> LaunchAsync(
        string projectName, string ticketId, bool preview, CancellationToken ct)
    {
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project)) return SpecDerivationLaunch.UnknownProject(projectName);
        if (project.Repos.Count == 0) return SpecDerivationLaunch.Refused(SpecDerivationTexts.NoRepos);
        var ticket = new TicketId(ticketId.TrimStart('#'));
        var lockKey = $"agentsmith:derive-launch:{project.Name}:{ticket.Value}";
        var token = await launchLock.TryAcquireAsync(lockKey, LaunchLockTtl, ct);
        if (token is null) return SpecDerivationLaunch.Refused(SpecDerivationTexts.BeingStarted);
        try { return await GuardAndLaunchAsync(project, ticket, preview, ct); }
        finally { await launchLock.ReleaseAsync(lockKey, token, CancellationToken.None); }
    }

    private async Task<SpecDerivationLaunch> GuardAndLaunchAsync(
        ResolvedProject project, TicketId ticket, bool preview, CancellationToken ct)
    {
        if (await gate.RefusalAsync(project.Name, ticket.Value, ct) is { } inFlight) return SpecDerivationLaunch.Refused(inFlight);
        var taken = await lease.TryClaimAsync(project.Name, ticket, ct);
        if (taken != LeaseClaimOutcome.Claimed) return SpecDerivationLaunch.Refused(SpecDerivationTexts.Held);
        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        var decision = await admission.TryAdmitAsync(
            project, PipelinePresets.SpecDeriveName, runId, ContextReadBranches.ForBranch(TicketBranchNamer.Compose(ticket)), ct);
        if (!decision.Admitted)
        {
            await lease.ReleaseAsync(project.Name, ticket, runId: null, CancellationToken.None);
            return SpecDerivationLaunch.NoCapacity(decision.Reason!);
        }
        return await RecordAndDispatchAsync(project, ticket, runId, preview, ct);
    }

    private async Task<SpecDerivationLaunch> RecordAndDispatchAsync(
        ResolvedProject project, TicketId ticket, string runId, bool preview, CancellationToken ct)
    {
        var platform = project.Tracker.Type.ToString().ToLowerInvariant();
        await runs.CreateQueuedRunAsync(runId, project.Name, ticket.Value, platform,
            [.. project.Repos.Select(r => r.Name)], SpecDerivationTexts.QueuedSummary, ct);
        await dispatch.DispatchAsync(new PipelineRequest(
            project.Name, PipelinePresets.SpecDeriveName, TicketId: ticket, Headless: true, RunId: runId,
            Context: new Dictionary<string, object> { [ContextKeys.SpecDerivationPreview] = preview }), ct);
        logger.LogInformation("Spec derivation of {Project}/#{Ticket} started (run {RunId}, preview {Preview})",
            project.Name, ticket.Value, runId, preview);
        return SpecDerivationLaunch.Started(runId);
    }
}

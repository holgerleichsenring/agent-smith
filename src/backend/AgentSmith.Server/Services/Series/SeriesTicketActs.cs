using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Runs;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series master's acts on a member ticket, each through the path a person's act takes: a
/// never-worked ticket whose requirements are met gets a 'start' run, a worked one is retried through Retry, and a
/// message answers its parked run or goes into its conversation (<see cref="SeriesTicketAnswer"/>).
/// </summary>
public sealed class SeriesTicketActs(
    TicketProjects projects, UnmetTicketRequirements requirements, IPreviousAttemptReader attempts,
    RunRetry retry, SeriesTicketAnswer answers, Deployment.DeployLauncher deploys, SeriesRunCancel cancels,
    WaitingTicketStart waiting, IRedisClaimLock locks,
    ITicketLandingStore landings, DependentsWake wake, ISeriesWakes wakes) : ISeriesTicketActs // 2026-11-24-f9c5e, 96aea
{
    /// <summary>2026-11-24-f9c5e: each landed ticket's dependents woken through the landing's own wake, now the series' work is on
    /// the default branch — the outside ones were held until then (f9c5b).</summary>
    public async Task<int> WakeDependentsAsync(string project, IReadOnlyList<string> ticketIds, CancellationToken cancellationToken)
    {
        if (projects.Of(project) is not { } resolved || ticketIds.Count == 0) return 0;
        var platform = resolved.Tracker.Type.ToString().ToLowerInvariant();
        var woken = 0;
        foreach (var landed in await landings.LandedAsync(resolved.Tracker.Name,
                     [.. ticketIds.Select(id => TicketKey.For(platform, id).Value)], cancellationToken))
            woken += await wake.WakeAsync(landed, cancellationToken, "reached the default branch with its series");
        return woken;
    }

    /// <summary>2026-11-26-5f6e: answered as what it came to — started with its run, queued, or refused with why.</summary>
    public async Task<SeriesStart> StartAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        if (projects.Of(project) is not { } resolved) return SeriesStart.Refused($"Project '{project}' is not configured.");
        // 2026-11-19-1b72a: a queued start or a live run is under way as much as an attempt is — no second start.
        if (await waiting.UnderWayAsync(project, ticketId, cancellationToken) is { } underWay)
            return SeriesStart.Refused($"Ticket {ticketId} was not started: {underWay}; nothing was done."); // 2026-11-26-a12eb
        var key = TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var hold = await requirements.ForStartAsync(resolved, key, cancellationToken);
        if (hold.Unmet.Count > 0) return SeriesStart.Refused($"Ticket {ticketId} was not started. {RequiresUnmetGate.Sentence(hold)}");
        // 2026-11-25-96aea: the start a landing's wake used to make — a ticket already in a trigger status is spawned too
        return await StartUnderLockAsync(resolved, ticketId, byName: true, cancellationToken);
    }

    /// <summary>2026-11-19-1b72a: a resume starts a never-worked ticket — the holds, the 'start' run and the claim through the
    /// poller's route (<see cref="WaitingTicketStart"/>); a ticket whose edge waits for a start by name is left to it.</summary>
    public async Task<SeriesStart> StartIfReadyAsync(string project, string ticketId, CancellationToken cancellationToken) =>
        projects.Of(project) is not { } resolved ? SeriesStart.Refused($"Project '{project}' is not configured.")
            : await StartUnderLockAsync(resolved, ticketId, byName: false, cancellationToken);

    private async Task<SeriesStart> StartUnderLockAsync(
        AgentSmith.Contracts.Models.Configuration.ResolvedProject resolved, string ticketId, bool byName, CancellationToken cancellationToken)
    {
        var project = resolved.Name;
        // Under the ticket's nudge lock, so a wake and a resume never both pass the under-way check.
        var key = ReworkNudgeWorker.LockKey(project, ticketId);
        if (await locks.TryAcquireAsync(key, ReworkNudgeRepository.ClaimLease, cancellationToken) is not { } token)
        {
            // 2026-11-25-96aea: nothing queues a start any more — the master is told again once the nudge is done
            await wakes.WakeAsync(project, ticketId, new SeriesWake(SeriesWakeKinds.Prerequisite,
                "its start found the ticket busy with another nudge; start_ticket starts it now")
                { Once = $"busy:{ticketId}:{DateTimeOffset.UtcNow:yyyyMMddHH}" }, cancellationToken); // once an hour at most
            return SeriesStart.Refused($"Ticket {ticketId} is busy with its nudge right now and was not started; you are told again shortly.");
        }
        try
        {
            return await waiting.StartAsync(resolved, ticketId, byName, cancellationToken);
        }
        catch (InvalidOperationException ex) { return SeriesStart.Refused(ex.Message); } // its start run ended first
        finally { await locks.ReleaseAsync(key, token, CancellationToken.None); }
    }

    public async Task<string> RetryAsync(string project, string ticketId, string principal, CancellationToken cancellationToken)
    {
        if (await attempts.LatestAsync(project, ticketId, null, cancellationToken) is not { } attempt)
            return $"Ticket {ticketId} has no run to retry; start it instead.";
        // 2026-11-22-50d7a: the series master acts here, named as its messages name it — 2026-11-25-9992h: the project's
        // machine identity; the approver's principal stays the authority the retry is checked against.
        var result = await retry.RetryAsync(
            attempt.RunId, principal, SeriesTicketMessage.AuthorOf(projects.Of(project)), RetryOrigin.Conversation, cancellationToken);
        return result.Kind == RunRetryResultKind.Requested
            ? $"Retry of ticket {ticketId}'s run {attempt.RunId} requested; a new attempt starts."
            : $"Ticket {ticketId} was not retried: {result.Text ?? result.Kind.ToString()}";
    }

    public Task<string> TellAsync(SeriesTell tell, CancellationToken cancellationToken) =>
        answers.TellAsync(tell, cancellationToken); // 2026-11-27-3532b

    public Task<string> CancelAsync(string runId, CancellationToken cancellationToken) => cancels.CancelAsync(runId, cancellationToken);

    /// <summary>2026-11-18-3863h: the series deploys only where no person approves; the binding is read here, never left
    /// to the model.</summary>
    public async Task<string> DeployAsync(
        string project, string context, string environment, string? version, string? ticketId, CancellationToken cancellationToken)
    {
        if (projects.Of(project) is not { } resolved) return $"Project '{project}' is not configured.";
        var (target, error) = DeploymentTarget.Of(resolved, new DeployRequest(context, null, environment, null));
        if (target is null) return $"Not deployed: {error}";
        if (target.Environment.Approval)
            return $"Not deployed: '{environment}' needs a person's approval, so a person deploys it from the ticket's page";
        var launch = await deploys.LaunchAsync(project, new Deployment.DeployLaunchRequest(context, environment, version, TicketId: ticketId), cancellationToken);
        return launch.Outcome == Deployment.DeployLaunchOutcome.Started
            ? $"Deploy of {context} to {environment} started (run {launch.RunId}); its outcome comes with the run."
            : $"Not deployed: {launch.Reason}";
    }
}

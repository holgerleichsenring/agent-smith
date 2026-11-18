using AgentSmith.Application.Services.Claim;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Series;
using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Server.Models;
using AgentSmith.Server.Services.Acts;
using AgentSmith.Server.Services.Rework;
using AgentSmith.Server.Services.Tickets;

namespace AgentSmith.Server.Services.Series;

/// <summary>
/// 2026-11-17-0207a: the series master's acts on a member ticket, each through the path a person's act takes: a
/// never-worked ticket whose requirements are met gets a 'start' run, a worked one is retried through Retry, and a
/// message goes into the ticket's conversation (<see cref="SeriesTicketMessage"/>).
/// </summary>
public sealed class SeriesTicketActs(
    TicketProjects projects, UnmetTicketRequirements requirements, TicketStartRun starts, IPreviousAttemptReader attempts,
    RunRetry retry, SeriesTicketMessage messages, Deployment.DeployLauncher deploys) : ISeriesTicketActs
{
    public async Task<string> StartAsync(string project, string ticketId, CancellationToken cancellationToken)
    {
        if (projects.Of(project) is not { } resolved) return $"Project '{project}' is not configured.";
        if (await attempts.LatestAsync(project, ticketId, null, cancellationToken) is { } attempt)
            return $"Ticket {ticketId} was worked already (run {attempt.RunId}, {attempt.Status}); retry or tell it instead.";
        var key = TicketKey.For(resolved.Tracker.Type.ToString().ToLowerInvariant(), ticketId).Value;
        var hold = await requirements.ForStartAsync(resolved, key, cancellationToken);
        if (hold.Unmet.Count > 0) return RequiresUnmetGate.Sentence(hold.Unmet, hold.GreenLifts);
        var (started, ended) = await starts.StartAsync(resolved, ticketId, mayStartRuns: true);
        return started is null ? $"Ticket {ticketId} was not started: {ended}"
            : $"Ticket {ticketId}: {started.State} — {started.Reason}";
    }

    public async Task<string> RetryAsync(string project, string ticketId, string principal, CancellationToken cancellationToken)
    {
        if (await attempts.LatestAsync(project, ticketId, null, cancellationToken) is not { } attempt)
            return $"Ticket {ticketId} has no run to retry; start it instead.";
        var result = await retry.RetryAsync(attempt.RunId, principal, RetryOrigin.Conversation, cancellationToken);
        return result.Kind == RunRetryResultKind.Requested
            ? $"Retry of ticket {ticketId}'s run {attempt.RunId} requested; a new attempt starts."
            : $"Ticket {ticketId} was not retried: {result.Text ?? result.Kind.ToString()}";
    }

    public Task<string> TellAsync(
        string project, string ticketId, string text, string principal, bool mayStartRuns, CancellationToken cancellationToken) =>
        messages.SendAsync(project, ticketId, text, principal, mayStartRuns, cancellationToken);

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

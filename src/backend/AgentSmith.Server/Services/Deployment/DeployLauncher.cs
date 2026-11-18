using AgentSmith.Application.Services;
using AgentSmith.Application.Services.Deployment;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Infrastructure.Persistence.Repositories;
using AgentSmith.Server.Services.Lifecycle;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Deployment;

/// <summary>
/// 2026-11-18-3863c: the one entry that starts a deploy — a person's, from the dashboard, and later the series run's.
/// It refuses what the config already answers (no such binding or environment, a version no manifest may carry), then
/// queues a 'deploy' run with string seeds, listed on the ticket it names. Whether the environment promotes is the
/// template's to say, so that refusal comes back as the run's outcome.
/// </summary>
public sealed class DeployLauncher(
    IConfigurationLoader configLoader, ServerContext serverContext, InitRunRepository runs, QueuedRunDispatch dispatch,
    TimeProvider timeProvider, ILogger<DeployLauncher> logger)
{
    public Task<DeployLaunch> LaunchAsync(string projectName, DeployLaunchRequest request, CancellationToken ct) =>
        StartAsync(projectName, request, null, ct);

    /// <summary>2026-11-18-3863h: the server's promotion — a deploy of an environment that promotes from one whose
    /// deploy of <paramref name="settledVersion"/> settled green. Not reachable from a person's request.</summary>
    public Task<DeployLaunch> PromoteAsync(string projectName, DeployLaunchRequest request, string settledVersion, CancellationToken ct) =>
        StartAsync(projectName, request with { Version = null }, settledVersion, ct);

    private async Task<DeployLaunch> StartAsync(string projectName, DeployLaunchRequest request, string? promoted, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var config = configLoader.LoadConfig(serverContext.ConfigPath);
        if (!config.Projects.TryGetValue(projectName, out var project)) return DeployLaunch.UnknownProject(projectName);
        var version = string.IsNullOrWhiteSpace(request.Version) ? null : request.Version.Trim();
        if (version is not null && !DeployVersionShape.IsValid(version))
            return DeployLaunch.Refused($"version '{version}' is not deliverable: a version is {DeployVersionShape.Description}");
        var deploy = new DeployRequest(request.Context, Blank(request.ContextRepo), request.Environment, version, promoted);
        var (target, error) = DeploymentTarget.Of(project, deploy);
        if (target is null) return DeployLaunch.Refused(error!);
        return DeployLaunch.Started(await QueueAsync(project, deploy, target, Blank(request.TicketId), ct));
    }

    private async Task<string> QueueAsync(
        ResolvedProject project, DeployRequest deploy, DeploymentTarget target, string? ticket, CancellationToken ct)
    {
        var runId = RunIdGenerator.Generate(timeProvider.GetUtcNow());
        var repos = target.RepoOf(project, target.Environment).Repo is { } repo ? [repo.Name] : project.Repos.Select(r => r.Name).ToList();
        await runs.CreateQueuedRunAsync(runId, project.Name, PipelinePresets.DeployName, repos,
            $"starting — delivering {deploy.Context} to {deploy.Environment}", ct);
        await dispatch.DispatchAsync(new PipelineRequest(project.Name, PipelinePresets.DeployName, TicketId: null,
            Headless: true, RunId: runId, Context: Seeds(deploy),
            ShownOnTicket: ticket is null ? null : new TicketId(ticket.TrimStart('#'))), ct);
        logger.LogInformation("Deploy of {Context} to {Environment} in {Project} launched (run {RunId})",
            deploy.Context, deploy.Environment, project.Name, runId);
        return runId;
    }

    /// <summary>The run's seeds — strings only, so the request survives the queue.</summary>
    public static Dictionary<string, object> Seeds(DeployRequest deploy)
    {
        var seeds = new Dictionary<string, object>
        {
            [ContextKeys.DeployContext] = deploy.Context, [ContextKeys.DeployEnvironment] = deploy.Environment,
        };
        if (deploy.ContextRepo is not null) seeds[ContextKeys.DeployContextRepo] = deploy.ContextRepo;
        if (deploy.Version is not null) seeds[ContextKeys.DeployVersion] = deploy.Version;
        if (deploy.PromotedVersion is not null) seeds[ContextKeys.DeployPromotedVersion] = deploy.PromotedVersion; // 3863h
        return seeds;
    }

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

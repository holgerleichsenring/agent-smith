using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Contracts;
using AgentSmith.Server.Services.ChatLaunch;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Server.Services.Webhooks;

/// <summary>
/// 2026-10-08-e8b9e: a PR command names a run the way a chat command does, so it starts through the
/// chat launchers — a ticket's run through the funnel and its claim (never the free-text path, which
/// overwrote a live run's lease), a ticketless one with the pull request's context and the admission's
/// pipeline, without a second parse. The launchers are scoped; this is a singleton beside the handlers.
/// </summary>
public sealed class PrCommandLaunch(
    IConfigurationLoader configLoader,
    ServerContext serverContext,
    IConfiguredRepoFinder repoFinder,
    IActiveRunLease leases,
    IServiceScopeFactory scopes,
    ISourceProviderFactory sources,
    ILogger<PrCommandLaunch> logger) : IPrCommandLaunch
{
    public async Task<WebhookResult> LaunchAsync(
        PrCommentCommand command, PipelineRequest request, CancellationToken ct, IReadOnlySet<string>? allowedProjects = null)
    {
        var owners = repoFinder.FindAll(configLoader.LoadConfig(serverContext.ConfigPath), command.Author.RepositoryUrl)
            .Where(o => allowedProjects is null || allowedProjects.Contains(o.ProjectName)).ToList();
        if (allowedProjects is not null && !string.IsNullOrEmpty(request.ProjectName) && !allowedProjects.Contains(request.ProjectName))
            return WebhookResult.NotHandled($"project {request.ProjectName} is not swept here");
        var owner = owners.FirstOrDefault(o => o.ProjectName == request.ProjectName) ?? (owners.Count == 1 ? owners[0] : null);
        if (owner is null) return WebhookResult.NotHandled($"repository {command.Author.RepositoryUrl} belongs to no single configured project");
        using var scope = scopes.CreateScope();
        var text = request.TicketId is { } ticket
            ? await TicketRunAsync(scope.ServiceProvider, owner, ticket.Value, request.PipelineName, ct)
            : await TicketlessRunAsync(scope.ServiceProvider, owner, command.PrNumber, request.PipelineName, ct);
        await SayAsync(owner.Repo, command.PrNumber, text, ct);
        return WebhookResult.HandledNoRoute();
    }

    private async Task<string> TicketRunAsync(IServiceProvider services, ConfiguredRepo owner, string ticketId, string pipeline, CancellationToken ct)
    {
        var result = await services.GetRequiredService<ChatTicketRunLauncher>().LaunchAsync(owner.ProjectName, ticketId, pipeline, ct);
        if (!result.TakenByAnother) return Answer(pipeline, result);
        var holder = await leases.GetByTicketAsync(owner.ProjectName, new TicketId(ticketId), ct);
        return holder is null ? PrCommandTexts.NoHolder(ticketId) : PrCommandTexts.WorkedOn(ticketId, holder.RunId);
    }

    private static async Task<string> TicketlessRunAsync(IServiceProvider services, ConfiguredRepo owner, string prNumber, string pipeline, CancellationToken ct)
    {
        var context = await services.GetRequiredService<ChatPrContextResolver>().ResolveAsync(owner.Repo, prNumber, ct);
        return Answer(pipeline, await services.GetRequiredService<ChatTicketlessRunLauncher>().LaunchAsync(owner.Project, pipeline, context, ct));
    }

    private static string Answer(string pipeline, ChatLaunchResult result) => result switch
    {
        { RunId: { } runId, WaitReason: { } reason } => PrCommandTexts.Queued(pipeline, runId, reason),
        { RunId: { } runId } => PrCommandTexts.Started(pipeline, runId),
        _ => PrCommandTexts.Refused(result.Refusal ?? "the run could not be started"),
    };

    private async Task SayAsync(RepoConnection repo, string prNumber, string text, CancellationToken ct)
    {
        try
        {
            if (sources.Create(repo) is IPrCommentProvider comments) await comments.PostCommentAsync(prNumber, text, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not answer the command on {Repo}#{Pr}", repo.Name, prNumber);
        }
    }
}

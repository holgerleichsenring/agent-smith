using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Reads Pending-lifecycle work items for an Azure DevOps project via ITicketProvider
/// and turns them into ClaimRequests. One WIQL listing per cycle.
/// </summary>
public sealed class AzureDevOpsWorkItemPoller(
    string projectName,
    ProjectConfig projectConfig,
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitioner transitioner,
    IPipelineConfigResolver pipelineConfigResolver,
    ILogger<AzureDevOpsWorkItemPoller> logger) : IEventPoller
{
    public string PlatformName => "AzureDevOps";
    public string ProjectName => projectName;
    public int IntervalSeconds => projectConfig.Polling.IntervalSeconds;

    public async Task<IReadOnlyList<ClaimRequest>> PollAsync(CancellationToken cancellationToken)
    {
        var provider = ticketFactory.Create(projectConfig.Tickets);
        var tickets = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, cancellationToken);

        if (tickets.Count == 0) return [];

        var trigger = projectConfig.AzuredevopsTrigger;
        var fallbackPipeline = TryResolveDefault();
        var requests = tickets
            .Select(t => new ClaimRequest(
                "AzureDevOps", projectName, t.Id,
                trigger is null
                    ? fallbackPipeline
                    : PipelineResolver.Resolve(trigger, t.Labels) ?? fallbackPipeline))
            .ToList();

        logger.LogInformation(
            "AzureDevOps poll for {Project}: building {Count} claim request(s) — {Tickets}",
            projectName, requests.Count,
            string.Join(", ", requests.Select(r => $"#{r.TicketId.Value}→{r.PipelineName}")));
        _ = transitioner;
        return requests;
    }

    private string TryResolveDefault()
    {
        try { return pipelineConfigResolver.ResolveDefaultPipelineName(projectConfig); }
        catch (InvalidOperationException) { return "fix-bug"; }
    }
}

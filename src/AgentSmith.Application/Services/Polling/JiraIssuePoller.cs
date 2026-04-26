using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Reads Pending-lifecycle issues for a Jira project via ITicketProvider
/// and turns them into ClaimRequests. One JQL search per cycle. Label-mode only;
/// native-status polling is deferred until JiraWorkflowCatalog gains probing.
/// </summary>
public sealed class JiraIssuePoller(
    string projectName,
    ProjectConfig projectConfig,
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitioner transitioner,
    ILogger<JiraIssuePoller> logger) : IEventPoller
{
    public string PlatformName => "Jira";
    public string ProjectName => projectName;
    public int IntervalSeconds => projectConfig.Polling.IntervalSeconds;

    public async Task<IReadOnlyList<ClaimRequest>> PollAsync(CancellationToken cancellationToken)
    {
        var provider = ticketFactory.Create(projectConfig.Tickets);
        var tickets = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, cancellationToken);

        if (tickets.Count == 0) return [];

        var pipeline = projectConfig.JiraTrigger?.DefaultPipeline ?? "fix-bug";
        var requests = tickets
            .Select(t => new ClaimRequest("Jira", projectName, t.Id, pipeline))
            .ToList();

        logger.LogDebug("Jira poll for {Project}: {Count} pending candidates",
            projectName, requests.Count);
        _ = transitioner;
        return requests;
    }
}

using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Reads Pending-lifecycle issues for a GitLab project via ITicketProvider
/// and turns them into ClaimRequests. One Issues-API listing per cycle.
/// </summary>
public sealed class GitLabIssuePoller(
    string projectName,
    ProjectConfig projectConfig,
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitioner transitioner,
    ILogger<GitLabIssuePoller> logger) : IEventPoller
{
    public string PlatformName => "GitLab";
    public string ProjectName => projectName;
    public int IntervalSeconds => projectConfig.Polling.IntervalSeconds;

    public async Task<IReadOnlyList<ClaimRequest>> PollAsync(CancellationToken cancellationToken)
    {
        var provider = ticketFactory.Create(projectConfig.Tickets);
        var tickets = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, cancellationToken);

        if (tickets.Count == 0) return [];

        var trigger = projectConfig.GitlabTrigger;
        var requests = tickets
            .Select(t => new ClaimRequest(
                "GitLab", projectName, t.Id,
                trigger is null
                    ? "fix-bug"
                    : PipelineResolver.Resolve(trigger, t.Labels) ?? "fix-bug"))
            .ToList();

        logger.LogDebug("GitLab poll for {Project}: {Count} pending candidates",
            projectName, requests.Count);
        _ = transitioner;
        return requests;
    }
}

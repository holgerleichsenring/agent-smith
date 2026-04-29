using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Models;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Application.Services.Polling;

/// <summary>
/// Reads Pending-lifecycle tickets for a GitHub project via ITicketProvider and turns
/// them into ClaimRequests. Pipeline resolution uses the project's github_trigger
/// (default_pipeline, falling back to "fix-bug"). Rate-limit aware: one listing per cycle.
/// </summary>
public sealed class GitHubIssuePoller(
    string projectName,
    ProjectConfig projectConfig,
    ITicketProviderFactory ticketFactory,
    ITicketStatusTransitioner transitioner,
    ILogger<GitHubIssuePoller> logger) : IEventPoller
{
    public string PlatformName => "GitHub";
    public string ProjectName => projectName;
    public int IntervalSeconds => projectConfig.Polling.IntervalSeconds;

    public async Task<IReadOnlyList<ClaimRequest>> PollAsync(CancellationToken cancellationToken)
    {
        var provider = ticketFactory.Create(projectConfig.Tickets);
        var tickets = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, cancellationToken);

        if (tickets.Count == 0) return [];

        var trigger = projectConfig.GithubTrigger;
        var requests = tickets
            .Select(t => new ClaimRequest(
                "GitHub", projectName, t.Id,
                trigger is null
                    ? "fix-bug"
                    : PipelineResolver.Resolve(trigger, t.Labels) ?? "fix-bug"))
            .ToList();

        logger.LogInformation(
            "GitHub poll for {Project}: building {Count} claim request(s) — {Tickets}",
            projectName, requests.Count,
            string.Join(", ", requests.Select(r => $"#{r.TicketId.Value}→{r.PipelineName}")));
        _ = transitioner; // reserved for per-ticket status refinement in follow-up
        return requests;
    }
}

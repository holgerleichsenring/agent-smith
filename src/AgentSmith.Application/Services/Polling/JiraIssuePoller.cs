using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Contracts.Providers;
using AgentSmith.Contracts.Services;
using AgentSmith.Domain.Entities;
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
        var trigger = projectConfig.JiraTrigger;

        var pendingTickets = await provider.ListByLifecycleStatusAsync(
            TicketLifecycleStatus.Pending, cancellationToken);
        var discovered = await DiscoverNewAsync(provider, trigger, cancellationToken);
        var merged = MergeAndFilter(pendingTickets, discovered);

        if (merged.Count == 0) return [];

        var requests = merged
            .Select(t => new ClaimRequest(
                "Jira", projectName, t.Id,
                trigger is null
                    ? "fix-bug"
                    : PipelineResolver.Resolve(trigger, t.Labels) ?? "fix-bug"))
            .ToList();

        logger.LogInformation(
            "Jira poll for {Project}: building {Count} claim request(s) — {Tickets}",
            projectName, requests.Count,
            string.Join(", ", requests.Select(r => $"#{r.TicketId.Value}→{r.PipelineName}")));
        _ = transitioner;
        return requests;
    }

    private async Task<IReadOnlyList<Ticket>> DiscoverNewAsync(
        ITicketProvider provider,
        WebhookTriggerConfig? trigger,
        CancellationToken ct)
    {
        if (trigger is null || trigger.PipelineFromLabel.Count == 0) return [];
        var labels = trigger.PipelineFromLabel.Keys.ToArray();
        var found = await provider.ListByLabelsInOpenStatesAsync(labels, ct) ?? [];
        var claimable = LifecyclePollFilter.KeepClaimable(found).ToList();
        logger.LogInformation(
            "Jira Discovery for {Project}: scanned [{Labels}] → {Total} candidate(s), {Claimable} claimable",
            projectName, string.Join(", ", labels), found.Count, claimable.Count);
        return claimable;
    }

    private static IReadOnlyList<Ticket> MergeAndFilter(
        IReadOnlyList<Ticket> pending,
        IReadOnlyList<Ticket> discovered)
    {
        var merged = new Dictionary<string, Ticket>();
        foreach (var t in pending) merged[t.Id.Value] = t;
        foreach (var t in discovered) merged.TryAdd(t.Id.Value, t);
        return [.. merged.Values];
    }
}

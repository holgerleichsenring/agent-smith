using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.PipelineHarness.Presets;

/// <summary>
/// 2026-09-25-8e51e: the rewrite capability for harness trackers. No preset amends a ticket — an
/// amendment is approved by a person on a bound conversation — so this records what it was asked
/// to write and says it landed, which is what a preset that never asks needs.
/// </summary>
internal sealed class HarnessTicketRewriter : ITicketRewriter
{
    public List<string> Regions { get; } = [];

    public Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken)
    {
        lock (Regions) Regions.Add(region);
        return Task.FromResult(TicketRewriteResult.Ok);
    }
}

/// <summary>
/// 2026-09-27-5c1ea: the harness never searches a tracker, so the search answers "no matches"
/// rather than a refusal — a refusal would be a claim about a tracker nobody asked.
/// </summary>
internal sealed class HarnessTicketSearch : ITicketSearch
{
    public Task<TicketSearchResult> SearchAsync(string text, int limit, CancellationToken cancellationToken) =>
        Task.FromResult(TicketSearchResult.None);
}

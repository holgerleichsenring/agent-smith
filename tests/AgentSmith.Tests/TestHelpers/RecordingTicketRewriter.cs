using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;

namespace AgentSmith.Tests.TestHelpers;

/// <summary>
/// 2026-09-25-8e51e: a rewriter that keeps what it was handed, so a test can assert the REGION an
/// amendment would write without a tracker. The answer is settable: a refusal is as much a case
/// as a write, and Jira's is the one the phase is about.
/// </summary>
public sealed class RecordingTicketRewriter(TicketRewriteResult? answer = null) : ITicketRewriter
{
    /// <summary>The regions written, in order — one per amendment that reached the tracker.</summary>
    public List<string> Regions { get; } = [];

    /// <summary>The ticket ids it was asked about.</summary>
    public List<string> Tickets { get; } = [];

    public Task<TicketRewriteResult> RewriteRegionAsync(
        TicketId ticketId, string region, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(ticketId);
        Tickets.Add(ticketId.Value);
        var result = answer ?? TicketRewriteResult.Ok;
        if (result.Rewritten) Regions.Add(region);
        return Task.FromResult(result);
    }
}

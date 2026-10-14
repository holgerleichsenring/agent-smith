using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-8762l: one finding with the evidence string as stored.</summary>
public sealed record TicketReviewFindingView(string Text, string Evidence)
{
    public static TicketReviewFindingView Of(SpecReviewFinding finding) => new(finding.Text, finding.Evidence);
}

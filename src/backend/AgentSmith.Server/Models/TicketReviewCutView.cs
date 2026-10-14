using AgentSmith.Contracts.Events;

namespace AgentSmith.Server.Models;

/// <summary>2026-10-13-8762l: one cut as stored — its member on a series round, the evidence string as given.</summary>
public sealed record TicketReviewCutView(string? SpecId, string Register, string Quote, string? Replacement, string Evidence)
{
    public static TicketReviewCutView Of(SpecReviewCut cut) => new(cut.SpecId, cut.Register, cut.Quote, cut.Replacement, cut.Evidence);
}

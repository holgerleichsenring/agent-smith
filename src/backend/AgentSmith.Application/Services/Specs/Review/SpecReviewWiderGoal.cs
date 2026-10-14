using AgentSmith.Contracts.Specs;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>2026-10-13-8762g: what a spec's reviewers are told it serves — the ticket as the cut review reads
/// it, and the series' goal when an epic set one. Null when neither says anything.</summary>
public static class SpecReviewWiderGoal
{
    public static string? Of(Ticket? ticket, SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var parts = new[] { ticket is null ? null : SpecCutReviewTicketText.Of(ticket), set.Goal }
            .Where(part => !string.IsNullOrWhiteSpace(part)).ToList();
        return parts.Count == 0 ? null : string.Join("\n\n", parts);
    }
}

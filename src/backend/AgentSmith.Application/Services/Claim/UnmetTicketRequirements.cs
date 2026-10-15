using AgentSmith.Contracts.Services;
using AgentSmith.Contracts.Specs;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.Claim;

/// <summary>
/// 2026-10-14-dbceh: the one answer to "is this ticket held by a ticket it requires?" — asked by
/// <see cref="RequiresUnmetGate"/> at every door and again by the wake under the dependent's lock, so the
/// wake never starts what the gate would refuse. A requirement is met only when the required ticket LANDED,
/// merged or marked done by hand.
/// </summary>
public sealed class UnmetTicketRequirements(
    ITicketRequirementStore requirements, ITicketLandingStore landings, ISpecApprovalStore approvals)
{
    /// <summary>
    /// Whether the ticket has requirement rows, and the edges whose required ticket has not landed, in the
    /// stored order. Without rows it answers (false, []) after the one requirement read and reads no landing,
    /// so a ticket without rows is told apart from one whose every requirement landed.
    /// </summary>
    public async Task<(bool HasRequirements, IReadOnlyList<TicketRequirementEdge> Unmet)> OfAsync(
        string tracker, string ticketKey, CancellationToken cancellationToken)
    {
        var edges = await requirements.RequiredOfAsync(tracker, ticketKey, cancellationToken);
        if (edges.Count == 0) return (false, []);
        var landed = (await landings.LandedAsync(tracker, [.. edges.Select(e => e.RequiredTicketKey)], cancellationToken))
            .Select(l => l.TicketKey).ToHashSet(StringComparer.Ordinal);
        return (true, [.. edges.Where(e => !landed.Contains(e.RequiredTicketKey))]);
    }

    /// <summary>
    /// Whether the ticket's filing finished: its ticket-text approval record is stored, which filing writes
    /// last, after the ticket and its edges. Asked only for a ticket WITH requirement rows — an ordinary
    /// ticket has no approval record and is never asked.
    /// </summary>
    public async Task<bool> FiledCompletelyAsync(string tracker, string ticketKey, CancellationToken cancellationToken) =>
        await approvals.GetAsync(tracker, ticketKey, cancellationToken) is not null;
}

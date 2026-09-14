using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-09-13-a72a: puts an epic's children in DEPENDENCY order before they are filed.
/// <para>
/// RequiresEdgeChecker validates only that a requires: edge points at a SIBLING — not that
/// the sibling comes first — so a forward edge is legal today and the filer creates children
/// in list order. A child filed before its predecessor exists cannot carry that predecessor's
/// ticket id, the spawn funnel then reads no predecessor, and the child runs first: an
/// invisible failure rather than an error. There is no repair pass either — ITicketProvider
/// has no member that edits a description or adds a label to an existing ticket — so the
/// order has to be right at creation time.
/// </para>
/// <para>
/// Kahn's algorithm, ties broken by the cut's own order so an epic that is already ordered
/// is filed exactly as it was proposed. An epic whose edges leave a node unreachable is
/// REFUSED: it cannot be filed in an order that holds.
/// </para>
/// </summary>
public sealed class EpicChildOrderer
{
    public EpicChildOrder Order(IReadOnlyList<PhaseDraft> children)
    {
        ArgumentNullException.ThrowIfNull(children);
        var siblings = children.Select(c => c.PhaseId).ToHashSet(StringComparer.Ordinal);
        var pending = new List<PhaseDraft>(children);
        var placed = new HashSet<string>(StringComparer.Ordinal);
        var ordered = new List<PhaseDraft>(children.Count);

        while (pending.Count > 0)
        {
            // Free text passes through: only an edge naming a SIBLING orders anything.
            var next = pending.FirstOrDefault(c =>
                c.Requires.Where(siblings.Contains).All(placed.Contains));
            if (next is null)
                return new EpicChildOrder(
                    children,
                    "the epic's requires: edges cannot be put in an order — "
                    + $"'{string.Join("', '", pending.Select(c => c.PhaseId))}' each wait on "
                    + "another slice in this epic");
            ordered.Add(next);
            placed.Add(next.PhaseId);
            pending.Remove(next);
        }
        return new EpicChildOrder(ordered, Error: null);
    }
}

using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-13-a72a: files an epic — the parent record first, then every child, then the
/// parent comment listing what was filed. Extracted from OutcomeTicketFiler, which now
/// routes the three outcome kinds and owns none of them.
/// <para>
/// The children are filed in DEPENDENCY order (see <see cref="EpicChildOrderer"/>), because
/// a child's predecessor stamp can only name a ticket that already exists, and each child
/// carries its parent and its predecessors as labels — the only field the spawn funnel sees
/// on both the polling and the webhook path.
/// </para>
/// </summary>
public sealed class EpicTicketFiler(PhaseTicketRenderer renderer, EpicChildOrderer orderer)
{
    public async Task FileAsync(
        ITicketProvider provider, EpicOutcome epic, List<FiledTicket> filed, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(epic);
        var order = orderer.Order(epic.Children);
        if (order.Error is not null)
            throw new InvalidOperationException($"The epic cannot be filed: {order.Error}.");

        var parentContent = renderer.RenderEpicParent(epic.Parent, order.Children);
        // 2026-09-13-a3f1: the parent is the record of a cut, never work.
        var parent = await provider.CreateAsync(
            parentContent.Title, parentContent.Body, [PhaseTicketRenderer.EpicLabel], ct);
        filed.Add(new FiledTicket(parent.Reference, parentContent.Title));

        var childRefs = new List<string>();
        var ticketIdByPhaseId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in order.Children)
        {
            var content = renderer.RenderChildRequirement(child, parent.Reference);
            var created = await provider.CreateAsync(
                content.Title, content.Body, ChildLabels(child, parent, ticketIdByPhaseId), ct);
            ticketIdByPhaseId[child.PhaseId] = created.Id.Value;
            filed.Add(new FiledTicket(created.Reference, content.Title));
            childRefs.Add($"{created.Reference} — `{child.PhaseId}` {child.Goal}");
        }
        await provider.UpdateStatusAsync(
            parent.Id, $"Slices filed:\n{string.Join("\n", childRefs.Select(r => $"- {r}"))}", ct);
    }

    // A sibling edge names a phase id; the label must name the TICKET the sibling was filed
    // as, because that is what the funnel can read back from the tracker. Dependency order
    // is what makes every one of them already present in the map.
    private static IReadOnlyList<string> ChildLabels(
        PhaseDraft child, CreatedTicket parent, IReadOnlyDictionary<string, string> filedSiblings) =>
    [
        PhaseTicketRenderer.PhaseLabel,
        FiledTicketLabels.ParentStamp(parent.Id.Value),
        .. child.Requires
            .Where(filedSiblings.ContainsKey)
            .Select(r => FiledTicketLabels.PredecessorStamp(filedSiblings[r])),
    ];
}

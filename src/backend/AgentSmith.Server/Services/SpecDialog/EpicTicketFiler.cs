using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Providers;
using AgentSmith.Domain.Models;
using AgentSmith.Server.Models;
using Microsoft.Extensions.Logging;

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
/// <para>
/// 2026-09-17-042ea: each child is also linked to its parent through the tracker, for the people
/// reading it; the labels stay the stamp machines read. A link that does not land becomes a note,
/// never the filing's error — the tickets exist, and an error would offer a retry that files them twice.
/// </para>
/// </summary>
public sealed class EpicTicketFiler(
    PhaseTicketRenderer renderer, EpicChildOrderer orderer, ILogger<EpicTicketFiler> logger)
{
    public async Task FileAsync(
        ITicketProvider provider, EpicOutcome epic, List<FiledTicket> filed, List<string> notes,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(epic);
        var order = orderer.Order(epic.Children);
        if (order.Error is not null)
            throw new InvalidOperationException($"The epic cannot be filed: {order.Error}.");

        // 2026-09-13-ed5a: the parent records what the analysis read while it cut.
        var parentContent = renderer.RenderEpicParent(epic.Parent, order.Children, epic.Templates);
        // 2026-09-13-a3f1: the parent is the record of a cut, never work.
        var parent = await provider.CreateAsync(
            parentContent.Title, parentContent.Body, [PhaseTicketRenderer.EpicLabel], ct);
        filed.Add(new FiledTicket(parent.Reference, parentContent.Title));

        var childRefs = new List<string>();
        var siblingIds = order.Children.Select(c => c.PhaseId).ToHashSet(StringComparer.Ordinal);
        var ticketIdByPhaseId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var child in order.Children)
        {
            var content = renderer.RenderChildRequirement(child, siblingIds);
            var created = await provider.CreateAsync(
                content.Title, content.Body, ChildLabels(child, parent, ticketIdByPhaseId), ct);
            ticketIdByPhaseId[child.PhaseId] = created.Id.Value;
            filed.Add(new FiledTicket(created.Reference, content.Title));
            if (await LinkAsync(provider, created, parent, ct) is { Outcome: not ParentLinkOutcome.Linked } link)
                notes.Add($"{created.Reference} is not linked to its parent {parent.Reference}: {link.Reason}");
            childRefs.Add($"{created.Reference} — `{child.PhaseId}` {child.Goal}");
        }
        await provider.UpdateStatusAsync(
            parent.Id, $"Slices filed:\n{string.Join("\n", childRefs.Select(r => $"- {r}"))}", ct);
    }

    private async Task<ParentLinkResult> LinkAsync(
        ITicketProvider provider, CreatedTicket child, CreatedTicket parent, CancellationToken ct)
    {
        try
        {
            return await provider.LinkToParentAsync(child, parent.Id, ct);
        }
        // A timeout is a failed link; only the caller's own cancellation stops the filing.
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Linking {Child} to its parent {Parent} threw", child.Reference, parent.Reference);
            return ParentLinkResult.Failed(ex.Message);
        }
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

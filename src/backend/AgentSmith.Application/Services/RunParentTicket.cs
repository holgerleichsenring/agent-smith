using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// 2026-09-13-5cdf: one answer to "which epic record is this run a slice of?", read from
/// the ticket's own labels.
/// <para>
/// 2026-09-13-a72a stamped the parent on an epic CHILD as a reserved-prefix label, because
/// a label is the only place both the polling and the webhook path can read it without a
/// tracker round-trip. The base ladder and the work branch must name the same parent, so
/// they read it here rather than each reaching into the label list.
/// </para>
/// <para>
/// 2026-09-17-0e79d: the framework stamps no filed ticket any more — an epic's work ticket cuts
/// from its own base and its slice records carry no stamp at all — so this answers null for
/// everything it files, and names a parent only for a legacy child or a hand-stamped ticket.
/// </para>
/// </summary>
public static class RunParentTicket
{
    /// <summary>The parent's ticket id, or null when this run's ticket is not a slice.</summary>
    public static string? Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        return pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null
            ? FiledTicketLabels.ParentId(ticket.Labels)
            : null;
    }
}

using System.Text.Json;
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
/// <para>
/// 2026-11-17-9a39b: a STACKED run answers its unlanded prerequisite first — the claim stamped it, and its pushed
/// branch is this run's rung. It wins over a label parent: the prerequisite's branch was cut from that rung already.
/// </para>
/// </summary>
public static class RunParentTicket
{
    /// <summary>The ticket whose branch this run is cut from, or null when it cuts from its own base.</summary>
    public static string? Of(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (StackedOn(pipeline) is { } stacked) return stacked;
        return pipeline.TryGet<Ticket>(ContextKeys.Ticket, out var ticket) && ticket is not null
            ? FiledTicketLabels.ParentId(ticket.Labels)
            : null;
    }

    /// <summary>Whether the answer is a prerequisite's branch — a rung nobody may publish, only find.</summary>
    public static bool IsStacked(PipelineContext pipeline) => StackedOn(pipeline) is not null;

    private static string? StackedOn(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        if (!pipeline.TryGet<object>(ContextKeys.StackedOnTicket, out var value)) return null;
        var id = value switch
        {
            string text => text,
            JsonElement { ValueKind: JsonValueKind.String } element => element.GetString(),
            _ => null,
        };
        return string.IsNullOrWhiteSpace(id) ? null : id.Trim();
    }
}

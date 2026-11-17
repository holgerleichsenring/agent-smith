using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Application.Services.SpecDialog;

/// <summary>
/// 2026-11-17-dd05b: the region of a series as its specs say it, through the filing's one renderer — a lone spec as a
/// filed spec, several as their series under its goal. What a list ticket is filed with and what a 'ticket-body' run
/// re-renders from the branch, so a ticket body is never a free-text copy of what its specs say.
/// </summary>
public sealed class SeriesTicketRegion(SpecTicketRenderer renderer)
{
    public SpecTicketContent Render(
        string goal, string series, IReadOnlyList<SpecDraft> specs, string? conversation = null, string? labelNote = null)
    {
        ArgumentNullException.ThrowIfNull(specs);
        return specs.Count == 1
            ? renderer.RenderSpec(specs[0], conversation, labelNote)
            : renderer.RenderEpicParent(SeriesParentDraft.Of(series, goal), specs, null, conversation, labelNote, series);
    }
}

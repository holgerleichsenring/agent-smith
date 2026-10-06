using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Tickets;

namespace AgentSmith.Server.Services.SpecDialog;

/// <summary>
/// 2026-09-25-8e51e: what an amendment would put on the ticket — the set to record, and the
/// framework's REGION rendered exactly as a filing would render it.
/// <para>
/// Rendered by the same renderer with the same label note, so an amended ticket and a freshly
/// filed one are the same body. A second rendering here would be a second truth about what a
/// filed ticket looks like, and the two would disagree the first time either changed.
/// </para>
/// </summary>
/// <param name="Set">The phases to record under the ticket's key, in run order, re-id'd to the
/// ticket's series (2026-10-06-03c7c).</param>
/// <param name="Region">The whole framework region, markers included.</param>
/// <param name="Error">Why this proposal cannot amend anything; the other two are then empty.</param>
internal sealed record AmendedSpecification(
    FiledSeries Set, string Region, string? Error)
{
    /// <param name="vocabulary">The bound ticket's tracker's label names, so the note names the
    /// stamp that board actually carries — the one a filing on it wrote.</param>
    /// <param name="series">The base id the ticket's series already carries — kept, never re-minted.</param>
    internal static AmendedSpecification Of(
        OutcomeProposal proposal, string series, string conversation, AmendmentRendering rendering,
        TicketLabelVocabulary vocabulary) =>
        proposal switch
        {
            PhaseOutcome phase => FromPhase(
                rendering.Series.Under(series, [phase.Draft]), conversation, rendering, Note(vocabulary)),
            EpicOutcome epic => FromEpic(epic, series, conversation, rendering, Note(vocabulary)),
            // A bug ticket is filed from another renderer and carries no approved set, so there
            // is no region of ours on the bound ticket for it to replace.
            _ => Refused(
                $"An outcome of kind '{proposal.GetType().Name}' is not filed from an approved "
                + "specification, so it cannot amend a ticket."),
        };

    private static string? Note(TicketLabelVocabulary vocabulary) =>
        TicketLabelNote.For([vocabulary.ApprovedSetStamp], vocabulary.ApprovedSetStamp);

    private static AmendedSpecification FromPhase(
        FiledSeries set, string conversation, AmendmentRendering rendering, string? note) =>
        new(set, rendering.Renderer.RenderPhase(set.Drafts[0], conversation, note).Body, null);

    private static AmendedSpecification FromEpic(
        EpicOutcome epic, string series, string conversation, AmendmentRendering rendering, string? note)
    {
        // The same refusal the filing makes, before anything is written: a cut whose edges
        // cannot be ordered has no run order, and the body lists the slices in that order.
        var order = rendering.Orderer.Order(epic.Children);
        if (order.Error is not null)
            return Refused($"The amended cut cannot be ordered: {order.Error}.");
        var set = rendering.Series.Under(series, order.Children);
        return new(
            set,
            rendering.Renderer.RenderEpicParent(
                epic.Parent, set.Drafts, epic.Templates, conversation, note, series).Body,
            null);
    }

    private static AmendedSpecification Refused(string error) =>
        new(new FiledSeries(string.Empty, []), string.Empty, error);
}

using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

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
/// <param name="Set">The phases to record under the ticket's spec key, in run order.</param>
/// <param name="Region">The whole framework region, markers included.</param>
/// <param name="Error">Why this proposal cannot amend anything; the other two are then empty.</param>
internal sealed record AmendedSpecification(
    IReadOnlyList<PhaseDraft> Set, string Region, string? Error)
{
    private static readonly string[] Labels = [FiledTicketLabels.ApprovedSetStamp];

    internal static AmendedSpecification Of(
        OutcomeProposal proposal, string conversation,
        PhaseTicketRenderer renderer, EpicChildOrderer orderer) => proposal switch
        {
            PhaseOutcome phase => new(
                [phase.Draft],
                renderer.RenderPhase(phase.Draft, conversation, TicketLabelNote.For(Labels)).Body,
                null),
            EpicOutcome epic => FromEpic(epic, conversation, renderer, orderer),
            // A bug ticket is filed from another renderer and carries no approved set, so there
            // is no region of ours on the bound ticket for it to replace.
            _ => Refused(
                $"An outcome of kind '{proposal.GetType().Name}' is not filed from an approved "
                + "specification, so it cannot amend a ticket."),
        };

    private static AmendedSpecification FromEpic(
        EpicOutcome epic, string conversation, PhaseTicketRenderer renderer, EpicChildOrderer orderer)
    {
        // The same refusal the filing makes, before anything is written: a cut whose edges
        // cannot be ordered has no run order, and the body lists the slices in that order.
        var order = orderer.Order(epic.Children);
        if (order.Error is not null)
            return Refused($"The amended cut cannot be ordered: {order.Error}.");
        return new(
            order.Children,
            renderer.RenderEpicParent(
                epic.Parent, order.Children, epic.Templates, conversation,
                TicketLabelNote.For(Labels)).Body,
            null);
    }

    private static AmendedSpecification Refused(string error) => new([], string.Empty, error);
}

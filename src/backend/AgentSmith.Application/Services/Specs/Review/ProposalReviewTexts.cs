using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: the texts a review pass reads in a proposal, each as an exact key — a ticket list as its whole
/// list, a spec or an epic child as its id and yaml, an epic's parent with its contract. Any changed byte is a new key.
/// An answer, a bug or a comment holds no text a pass reads. 2026-11-13-3f79: each text also names the id its review
/// sits under and the key it is settled under (<see cref="ProposalReviewText"/>).
/// </summary>
internal static class ProposalReviewTexts
{
    /// <summary>The id and settled key of a ticket list's one text.</summary>
    public const string ListId = "list";

    public static IReadOnlyList<ProposalReviewText> Of(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return proposal switch
        {
            TicketGraphOutcome graph => [new(List(graph), ListId, ListId)],
            SpecOutcome spec => [Text(spec.Draft)],
            EpicOutcome epic => [.. epic.Children.Select(Text), new(Parent(epic), ProposalReviewTokens.Series, SettledParent(epic))],
            _ => [],
        };
    }

    public static string Draft(SpecDraft draft) => $"spec\n{draft.SpecId}\n{draft.Yaml}";

    public static string Parent(EpicOutcome epic) =>
        $"parent\n{epic.Parent.SpecId}\n{epic.Parent.Yaml}\n{JsonSerializer.Serialize(epic.Contract)}";

    /// <summary>The key an epic's parent is settled under: the series token and the parent's id.</summary>
    public static string SettledParent(EpicOutcome epic) => $"{ProposalReviewTokens.Series}\n{epic.Parent.SpecId}";

    private static ProposalReviewText Text(SpecDraft draft) => new(Draft(draft), draft.SpecId, draft.SpecId);

    private static string List(TicketGraphOutcome graph) => $"list\n{JsonSerializer.Serialize(graph.Tickets)}";
}

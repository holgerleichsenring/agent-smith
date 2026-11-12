using System.Text.Json;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-12-92e9b: the texts a review pass reads in a proposal, each as an exact key — a ticket list as its whole
/// list, a spec or an epic child as its id and yaml, an epic's parent with its contract. Any changed byte is a new key.
/// An answer, a bug or a comment holds no text a pass reads.
/// </summary>
internal static class ProposalReviewTexts
{
    public static IReadOnlyList<string> Of(OutcomeProposal proposal)
    {
        ArgumentNullException.ThrowIfNull(proposal);
        return proposal switch
        {
            TicketGraphOutcome graph => [List(graph)],
            SpecOutcome spec => [Draft(spec.Draft)],
            EpicOutcome epic => [.. epic.Children.Select(Draft), Parent(epic)],
            _ => [],
        };
    }

    public static string Draft(SpecDraft draft) => $"spec\n{draft.SpecId}\n{draft.Yaml}";

    public static string Parent(EpicOutcome epic) =>
        $"parent\n{epic.Parent.SpecId}\n{epic.Parent.Yaml}\n{JsonSerializer.Serialize(epic.Contract)}";

    private static string List(TicketGraphOutcome graph) => $"list\n{JsonSerializer.Serialize(graph.Tickets)}";
}

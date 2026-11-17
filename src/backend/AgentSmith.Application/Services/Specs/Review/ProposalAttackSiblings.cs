using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfa: the siblings a design turn's epic child is reviewed beside — every OTHER child of the proposal
/// the round reviews, read from the session's current proposal so a revised set reaches the next round. A lone spec
/// has none. 2026-11-17-5701a: a ticket's spec is reviewed beside the other specs of its ticket.
/// </summary>
internal static class ProposalAttackSiblings
{
    public static IReadOnlyList<SpecAttackSibling> Of(ProposalReviewSession session, SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);
        IReadOnlyList<SpecDraft> set = session.Proposal is EpicOutcome epic ? epic.Children
            : ProposalSpecSet.TicketOf(session.Proposal, draft.SpecId)?.Specs ?? [];
        return [.. set.Where(child => child.SpecId != draft.SpecId).Select(Sibling)];
    }

    private static SpecAttackSibling Sibling(SpecDraft child)
    {
        var scope = Scope(child.Yaml);
        return new SpecAttackSibling(child.SpecId, child.Goal, child.Repos,
            scope is null ? null : OutcomeYamlReader.GetString(scope, "in"),
            scope is null ? null : OutcomeYamlReader.GetString(scope, "out"));
    }

    private static IReadOnlyDictionary<string, object?>? Scope(string yaml)
    {
        try
        {
            return OutcomeYamlReader.GetMap(OutcomeYamlReader.ReadMap(yaml), "scope");
        }
        catch (YamlDotNet.Core.YamlException)
        {
            return null;
        }
    }
}

using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Specs.Review;

/// <summary>
/// 2026-11-11-2bdfa: the siblings a design turn's epic child is reviewed beside — every OTHER child of the proposal
/// the round reviews, read from the session's current proposal so a revised set reaches the next round. A lone spec
/// has none.
/// </summary>
internal static class ProposalAttackSiblings
{
    public static IReadOnlyList<SpecAttackSibling> Of(ProposalReviewSession session, SpecDraft draft)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(draft);
        if (session.Proposal is not EpicOutcome epic) return [];
        return [.. epic.Children.Where(child => child.SpecId != draft.SpecId).Select(Sibling)];
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

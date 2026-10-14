using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the body of the pull request opened at the spec commit, and the
/// discarded list that stays on it to the end (the per-spec status table is
/// <see cref="SpecStatusTable"/>).
/// <para>
/// Non-blocking ratification: this is "here is how I understood it" and the run
/// proceeds. The risk that a wrong reading gets built before anyone objects is
/// accepted, and it is bounded by exactly these two sections being visible here.
/// </para>
/// </summary>
public static class SpecPrBody
{
    /// <summary>2026-10-12-f5f7e: where the cross-link puts the sibling pull requests — on a spec PR from the first write.</summary>
    public const string SiblingMarker = "<!-- agentsmith:sibling-prs -->";

    public static string BuildInitial(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var sb = new StringBuilder();
        sb.AppendLine("> 🚧 **Work in progress** — opened at the spec commit so the derivation can be");
        sb.AppendLine($"> reviewed while the run is still working. The specs live in `{SeriesPaths.Planned}/`;");
        sb.AppendLine("> a correcting comment on the ticket amends an unexecuted spec or re-cuts the");
        sb.AppendLine("> unexecuted tail — an executed spec is never edited.").AppendLine().AppendLine(Rework.ReworkRule.PrBodyLine);
        sb.AppendLine();
        sb.AppendLine(RenderSpecs(set));
        sb.AppendLine();
        sb.AppendLine(RenderDiscarded(set));
        sb.AppendLine(SiblingMarker);
        return sb.ToString();
    }

    /// <summary>The derived cut, one line per spec — what this run will work through.</summary>
    public static string RenderSpecs(SpecSet set)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Derived specs");
        sb.AppendLine();
        if (set.Specs.Count == 0)
        {
            sb.AppendLine("_No spec was derived._");
            return sb.ToString();
        }
        foreach (var spec in set.Specs)
            sb.AppendLine($"1. **{spec.SpecId}** — {spec.Draft.Goal}");
        return sb.ToString();
    }

    /// <summary>
    /// What the derivation deliberately left out. A human checks this in seconds;
    /// a coverage percentage would be optimised against the moment it is measured.
    /// </summary>
    public static string RenderDiscarded(SpecSet set)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Discarded from the ticket");
        sb.AppendLine();
        if (set.TicketPinnedWhole)
        {
            sb.AppendLine(
                "_The accounting could not be produced, so nothing was discarded: the whole "
                + "ticket is carried by a single spec._");
            return sb.ToString();
        }
        sb.AppendLine(SpecAccountingBuilder.RenderDiscardedForPullRequest(set.Accounting));
        return sb.ToString();
    }
}

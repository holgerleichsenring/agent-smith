using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the body of the pull request opened at the spec commit, and the two
/// sections that stay on it to the end — the discarded list and the per-spec
/// status table.
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

    /// <summary>
    /// The per-spec status table. A sequence that stopped mid-way leaves a
    /// HALF-MIGRATED repository — worse than not having started — so the pull request
    /// states which specs are through, which failed and on what, and which never ran.
    /// </summary>
    /// <param name="delivered">p0439: true when the done specs are delivered as a shortfall —
    /// the branch then carries exactly them, so the table is a delivery note, not a warning.</param>
    public static string RenderStatus(SpecSequenceProgress progress, bool delivered = false)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var sb = new StringBuilder();
        var halfMigrated = progress.IsPartial && !delivered;
        sb.AppendLine(halfMigrated
            ? "## ⛔ Half-migrated — DO NOT MERGE"
            : "## Spec status");
        sb.AppendLine();
        if (halfMigrated)
        {
            sb.AppendLine(
                "This sequence did not run to the end. The repository is in a PARTIAL state: "
                + "some specs are applied and others are not. Merging it ships exactly that.");
            sb.AppendLine();
        }
        else if (progress.IsPartial)
        {
            sb.AppendLine(
                "This sequence did not run to the end. The specs marked done are built, verified "
                + "and what this pull request delivers; the others are listed under \"Not delivered\".");
            sb.AppendLine();
        }
        sb.AppendLine("| Spec | Goal | Status |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var spec in progress.Specs)
            sb.AppendLine($"| {spec.SpecId} | {spec.Goal} | {Describe(spec)} |");
        return sb.ToString();
    }

    private static string Describe(SpecProgress spec) => spec.State switch
    {
        SpecRunState.Done => spec.Note is null ? "✅ done" : $"✅ done — {spec.Note}",
        SpecRunState.Failed => $"❌ failed — {spec.FailingCommand ?? "verification red"}",
        SpecRunState.HandedBack =>
            $"↩️ handed back, never built — {spec.FailingCommand ?? "a premise no longer holds"}",
        SpecRunState.InProgress => "⏳ started, not finished",
        _ => spec.Note is null ? "⬜ not started" : $"⬜ not started — {spec.Note}",
    };
}

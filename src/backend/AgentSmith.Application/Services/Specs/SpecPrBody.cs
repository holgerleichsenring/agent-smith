using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the body of the pull request opened at the spec commit, and the two
/// sections that stay on it to the end — the discarded list and the per-phase
/// status table.
/// <para>
/// Non-blocking ratification: this is "here is how I understood it" and the run
/// proceeds. The risk that a wrong reading gets built before anyone objects is
/// accepted, and it is bounded by exactly these two sections being visible here.
/// </para>
/// </summary>
public static class SpecPrBody
{
    public static string BuildInitial(SpecSet set)
    {
        ArgumentNullException.ThrowIfNull(set);
        var key = new SpecSetKey(set.Key);
        var sb = new StringBuilder();
        sb.AppendLine("> 🚧 **Work in progress** — opened at the spec commit so the derivation can be");
        sb.AppendLine($"> reviewed while the run is still working. The specs live in `{key.Directory}/`;");
        sb.AppendLine("> a correcting comment on the ticket amends an unexecuted phase or re-cuts the");
        sb.AppendLine("> unexecuted tail — an executed phase is never edited.");
        sb.AppendLine();
        sb.AppendLine(RenderPhases(set));
        sb.AppendLine();
        sb.AppendLine(RenderDiscarded(set));
        return sb.ToString();
    }

    /// <summary>The derived cut, one line per phase — what this run will work through.</summary>
    public static string RenderPhases(SpecSet set)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Derived phases");
        sb.AppendLine();
        if (set.Phases.Count == 0)
        {
            sb.AppendLine("_No phase was derived._");
            return sb.ToString();
        }
        foreach (var phase in set.Phases)
            sb.AppendLine($"1. **{phase.PhaseId}** — {phase.Draft.Goal}");
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
                + "ticket is carried by a single phase._");
            return sb.ToString();
        }
        sb.AppendLine(SpecAccountingBuilder.RenderDiscardedForPullRequest(set.Accounting));
        return sb.ToString();
    }

    /// <summary>
    /// The per-phase status table. A sequence that stopped mid-way leaves a
    /// HALF-MIGRATED repository — worse than not having started — so the pull request
    /// states which phases are through, which failed and on what, and which never ran.
    /// </summary>
    public static string RenderStatus(SpecSequenceProgress progress)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var sb = new StringBuilder();
        sb.AppendLine(progress.IsPartial
            ? "## ⛔ Half-migrated — DO NOT MERGE"
            : "## Phase status");
        sb.AppendLine();
        if (progress.IsPartial)
        {
            sb.AppendLine(
                "This sequence did not run to the end. The repository is in a PARTIAL state: "
                + "some phases are applied and others are not. Merging it ships exactly that.");
            sb.AppendLine();
        }
        sb.AppendLine("| Phase | Goal | Status |");
        sb.AppendLine("| --- | --- | --- |");
        foreach (var phase in progress.Phases)
            sb.AppendLine($"| {phase.PhaseId} | {phase.Goal} | {Describe(phase)} |");
        return sb.ToString();
    }

    private static string Describe(PhaseProgress phase) => phase.State switch
    {
        PhaseRunState.Done => "✅ done",
        PhaseRunState.Failed => $"❌ failed — {phase.FailingCommand ?? "verification red"}",
        PhaseRunState.InProgress => "⏳ started, not finished",
        _ => "⬜ not started",
    };
}

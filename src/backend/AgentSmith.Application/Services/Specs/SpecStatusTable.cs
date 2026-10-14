using System.Text;
using AgentSmith.Contracts.Specs;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// The per-spec status table. A sequence that stopped mid-way leaves a HALF-MIGRATED repository — worse than
/// not having started — so the pull request states which specs are through, which failed and on what, and which
/// never ran. 2026-10-13-8762u: an attempt that stopped at a series boundary is a series in progress, not a
/// failure: its heading says so and the specs it left read "next attempt".
/// </summary>
public static class SpecStatusTable
{
    /// <param name="delivered">p0439: true when the done specs are delivered as a shortfall —
    /// the branch then carries exactly them, so the table is a delivery note, not a warning.</param>
    public static string Render(SpecSequenceProgress progress, bool delivered = false)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var sb = new StringBuilder();
        var halfMigrated = progress.IsPartial && !delivered;
        var boundary = halfMigrated && progress.StoppedAtChunk;
        sb.AppendLine(boundary ? "## 🧩 Series in progress — do not merge until it is through"
            : halfMigrated ? "## ⛔ Half-migrated — DO NOT MERGE"
            : "## Spec status");
        sb.AppendLine();
        if (boundary)
        {
            sb.AppendLine(
                "This attempt built its share of the series and stopped at a boundary. The specs marked "
                + "\"next attempt\" are not started; the next attempt continues with them on this branch.");
            sb.AppendLine();
        }
        else if (halfMigrated)
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
            sb.AppendLine($"| {spec.SpecId} | {spec.Goal} | {Describe(spec, progress.StoppedAtChunk)} |");
        return sb.ToString();
    }

    private static string Describe(SpecProgress spec, bool atBoundary) => spec.State switch
    {
        SpecRunState.Done => spec.Note is null ? "✅ done" : $"✅ done — {spec.Note}",
        SpecRunState.Failed => $"❌ failed — {spec.FailingCommand ?? "verification red"}",
        SpecRunState.HandedBack =>
            $"↩️ handed back, never built — {spec.FailingCommand ?? "a premise no longer holds"}",
        SpecRunState.InProgress => "⏳ started, not finished",
        _ when spec.NextAttempt && atBoundary => "⏭️ next attempt",
        _ => spec.Note is null ? "⬜ not started" : $"⬜ not started — {spec.Note}",
    };
}

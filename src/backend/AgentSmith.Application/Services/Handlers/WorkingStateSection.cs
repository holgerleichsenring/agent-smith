using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// The continuity carry rendered into a re-engagement pass and into the compaction
/// pin — the decisions committed so far, the last build/test tail (p0341c) and the
/// paths the run has changed (p0411). Pure text over values, so it is unit-testable
/// in isolation.
/// </summary>
internal static class WorkingStateSection
{
    // The state block is a PROMPT: it is re-sent on every pass, so it stays a
    // file-count plus paths. A run that touches more files than this reports the
    // count and the first paths — never an unbounded list, never a diff body.
    private const int MaxListedPaths = 30;

    internal static string Build(
        IReadOnlyList<PlanDecision> decisions, MasterVerification? verification,
        IReadOnlyList<string>? changedPaths = null,
        IReadOnlyList<string>? stagedRegistries = null)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("## Working state (carry this forward)");
        AppendDecisions(sb, decisions);
        var tail = verification?.Summary;
        sb.AppendLine("Last build/test: "
            + (string.IsNullOrWhiteSpace(tail)
                ? $"status {verification?.Status.ToString() ?? "not yet run"}"
                : tail));
        AppendChangedFiles(sb, changedPaths);
        AppendStagedRegistries(sb, stagedRegistries);
        sb.AppendLine();
        return sb.ToString();
    }

    private static void AppendDecisions(
        System.Text.StringBuilder sb, IReadOnlyList<PlanDecision> decisions)
    {
        if (decisions is not { Count: > 0 })
        {
            sb.AppendLine("Decisions committed so far: (none logged yet)");
            return;
        }
        sb.AppendLine("Decisions committed so far:");
        foreach (var d in decisions.Take(12))
            sb.AppendLine($"- [{d.Category}] {d.Decision}");
    }

    // p0422: what the framework provisioned, stated rather than left to be guessed. Run 22
    // skipped every private-feed package with "no credentials in sandbox" in its own
    // decisions.md, having never tried — the credentials were staged and the build had
    // already used them.
    private static void AppendStagedRegistries(
        System.Text.StringBuilder sb, IReadOnlyList<string>? staged)
    {
        if (staged is not { Count: > 0 }) return;
        sb.AppendLine("Package-feed credentials the framework staged for you (file — hosts):");
        foreach (var line in staged.Take(6)) sb.AppendLine("- " + line);
        sb.AppendLine(
            "A restore from these feeds is expected to work. If one fails, say what the "
            + "command reported — never record that credentials are absent without trying.");
    }

    // p0411: the working tree as the framework read it, so the changed-file question
    // is answered rather than asked. Null means it was not read for this render
    // (the compaction pin) — the line is then omitted rather than claiming "none".
    private static void AppendChangedFiles(
        System.Text.StringBuilder sb, IReadOnlyList<string>? changedPaths)
    {
        if (changedPaths is null) return;
        if (changedPaths.Count == 0)
        {
            sb.AppendLine("Changed files in the working tree: none yet.");
            return;
        }
        var listed = changedPaths.Take(MaxListedPaths).ToList();
        var more = changedPaths.Count - listed.Count;
        sb.AppendLine($"Changed files in the working tree ({changedPaths.Count}): "
            + string.Join(", ", listed)
            + (more > 0 ? $", … +{more} more" : string.Empty));
    }
}

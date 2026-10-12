using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0438: the blocks a spec prompt is assembled FROM — what the framework staged, what a
/// reviewer found outstanding, what the spec must satisfy. Extracted from
/// <see cref="SpecExecutionPromptFactory"/>, which composes them: deciding what a block
/// SAYS is a different job from deciding where it sits, and each block is worth a test of
/// its own — a repair pass that fails to name its criteria is a wasted pass.
/// </summary>
public static class SpecExecutionPromptBlocks
{
    /// <summary>
    /// p0422: what the framework staged for this run, in the FIRST prompt — not only in a
    /// re-engagement the master may never need. Run 23 finished in 28 rounds without
    /// re-engaging, never saw it, and skipped every private-feed package again with
    /// "no credentials in sandbox" in its own decisions.md.
    /// </summary>
    public static string StagedRegistries(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<string>>(ContextKeys.StagedRegistries, out var staged)
            || staged is not { Count: > 0 })
            return string.Empty;
        return "\n**Package-feed credentials staged for you (file — hosts):**\n"
            + string.Join("\n", staged.Take(6).Select(line => "- " + line))
            + "\nA restore from these feeds is expected to work. If one fails, report what the "
            + "command said — never record that credentials are absent without trying.";
    }

    /// <summary>
    /// p0438: what the accountant said is missing, quoted, in the repair pass's own prompt.
    /// The repair is only worth a master pass if the agent is told exactly which criteria
    /// the branch does not satisfy — a generic "try again" spends the pass and closes
    /// nothing.
    /// <para>
    /// A structure correction that follows a repair takes its place: the repair's work is done
    /// and committed, and the correction may change nothing but the units it names.
    /// </para>
    /// </summary>
    public static string OutstandingCriteria(PipelineContext pipeline)
    {
        if (Findings(pipeline).Count > 0) return string.Empty;
        if (!pipeline.TryGet<List<string>>(ContextKeys.OutstandingCriteria, out var outstanding)
            || outstanding is not { Count: > 0 })
            return string.Empty;
        return "\n**This is a REPAIR pass. A fresh reviewer read the branch against this "
            + "spec's ratified criteria and found these NOT satisfied:**\n"
            + string.Join("\n", outstanding.Select(c => "- " + c))
            + "\nClose exactly these. The rest of the spec is already accounted for — "
            + "adding more is scope you were not given. If one of them cannot be satisfied, "
            + "say which and why rather than working around it.";
    }

    /// <summary>
    /// 2026-10-01-f5c3a: the draft's done list, already in its line form — the yaml is not
    /// re-read here, where a scenario entry would print as a dictionary's type name.
    /// </summary>
    public static string DoneCriteria(SpecDraft draft)
    {
        var done = draft.Done.Where(d => !string.IsNullOrWhiteSpace(d)).ToList();
        if (done.Count == 0) return string.Empty;
        var bullets = string.Join("\n", done.Select(d => $"- {d}"));
        return $"\n### Done criteria\n{bullets}\n";
    }

    /// <summary>
    /// The units the commit gate found over the principles' Limits, for the master's one
    /// correction. Empty unless that correction is under way; while it is, it is the only
    /// instruction the pass carries. 2026-10-12-fbbda: they are past the tolerance band, and the
    /// master is asked to reach the limit itself, not the band's end.
    /// </summary>
    public static string StructureFindings(PipelineContext pipeline)
    {
        var findings = Findings(pipeline);
        if (findings.Count == 0) return string.Empty;
        return "\n**This is a STRUCTURE correction. These units exceed the Limits in the principles "
            + "past their tolerance band (path, unit, metric, value > limit):**\n"
            + string.Join("\n", findings.Select(f => "- " + f))
            + "\nSplit each by responsibility until it is within its limit — the limit itself, not the "
            + "tolerance band. Change nothing else.";
    }

    private static IReadOnlyList<string> Findings(PipelineContext pipeline) =>
        pipeline.TryGet<List<string>>(ContextKeys.StructureFindings, out var findings) && findings is not null
            ? findings : [];
}

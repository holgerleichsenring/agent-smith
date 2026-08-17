using AgentSmith.Contracts.Commands;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;

namespace AgentSmith.Application.Services;

/// <summary>
/// p0438: the blocks a phase prompt is assembled FROM — what the framework staged, what a
/// reviewer found outstanding, what the phase must satisfy. Extracted from
/// <see cref="PhaseExecutionPromptFactory"/>, which composes them: deciding what a block
/// SAYS is a different job from deciding where it sits, and each block is worth a test of
/// its own — a repair pass that fails to name its criteria is a wasted pass.
/// </summary>
public static class PhaseExecutionPromptBlocks
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
    /// </summary>
    public static string OutstandingCriteria(PipelineContext pipeline)
    {
        if (!pipeline.TryGet<List<string>>(ContextKeys.OutstandingCriteria, out var outstanding)
            || outstanding is not { Count: > 0 })
            return string.Empty;
        return "\n**This is a REPAIR pass. A fresh reviewer read the branch against this "
            + "phase's ratified criteria and found these NOT satisfied:**\n"
            + string.Join("\n", outstanding.Select(c => "- " + c))
            + "\nClose exactly these. The rest of the phase is already accounted for — "
            + "adding more is scope you were not given. If one of them cannot be satisfied, "
            + "say which and why rather than working around it.";
    }

    public static string DoneCriteria(PhaseDraft draft)
    {
        var map = OutcomeYamlReader.ReadMap(draft.Yaml);
        var done = (OutcomeYamlReader.GetList(map, "done") ?? [])
            .Select(d => d?.ToString())
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .ToList();
        if (done.Count == 0) return string.Empty;
        var bullets = string.Join("\n", done.Select(d => $"- {d}"));
        return $"\n### Done criteria\n{bullets}\n";
    }
}

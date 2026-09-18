using AgentSmith.Contracts.Commands;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eh: what the phase reviews found — and which phases were never read — in the
/// pull-request body, one heading for the whole run, grouped by phase.
/// <para>
/// Nothing new is computed: the reports the framework kept are shown. A reviewer who reads
/// only the pull request otherwise never learns that a fresh instance read this diff and
/// objected — or that nobody did. A finding whose fix pass was reverted says so, because the
/// branch then carries the code the finding is about.
/// </para>
/// </summary>
public static class PhaseReviewSection
{
    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        // A review that ran and kept nothing is the quiet case and says nothing. A review that
        // did NOT run is the loud one — it is the absence of an answer, not an answer.
        var shown = PhaseReviewLedger.Current(pipeline).Phases
            .Where(p => p.Report.Findings.Count > 0 || !p.Report.Reviewed)
            .ToList();
        if (shown.Count == 0) return string.Empty;

        var lines = new List<string>
        {
            string.Empty, string.Empty, "## What the phase review still finds", string.Empty,
        };
        foreach (var phase in shown)
        {
            lines.Add($"**{phase.PhaseId}**");
            lines.Add(phase.Report.Reviewed
                ? string.Join("\n", phase.Report.Findings.Select(Row))
                : $"- {phase.Report.NotReviewedNote}");
            lines.Add(string.Empty);
        }
        return string.Join("\n", lines).TrimEnd();
    }

    /// <summary>Also the record's own wording, so the two never drift apart.</summary>
    public static string Row(PhaseFinding finding)
    {
        ArgumentNullException.ThrowIfNull(finding);
        var reverted = finding.Reverted is null ? string.Empty : $" — {finding.Reverted}";
        return $"- `{finding.Repository}/{finding.Path}:{finding.Line}` — {finding.Why} "
            + $"(rule: {finding.Rule}){reverted}";
    }

    /// <summary>The body of the record's own review block, or empty when nothing is recorded.</summary>
    public static string ForTheRecord(PhaseReviewReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        if (!report.Reviewed)
            return report.Why is null
                ? string.Empty
                : "## What the phase review still finds\n\n- " + report.NotReviewedNote + "\n";
        return report.Findings.Count == 0
            ? string.Empty
            : "## What the phase review still finds\n\n"
              + string.Join("\n", report.Findings.Select(Row)) + "\n";
    }
}

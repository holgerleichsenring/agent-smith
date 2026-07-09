using System.Text;
using AgentSmith.Application.Models;
using AgentSmith.Contracts.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Renders the pr-review markdown bodies: one inline comment per finding
/// group and the top-level summary (severity tally, folded excess findings,
/// pointer at the run's result.md). The idempotency markers are NOT rendered
/// here — PostPrCommentsHandler stamps them at dispatch time so the marker
/// convention lives in one place.
/// </summary>
public sealed class PrReviewCommentRenderer
{
    public string RenderInline(PrReviewFindingGroup group)
        => string.Join("\n\n---\n\n", group.Observations.Select(RenderFinding));

    public string RenderSummary(PrReviewFindingSelection selection, string runId)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## agent-smith PR review");
        sb.AppendLine();
        sb.AppendLine(selection.TotalFindings == 0
            ? "No findings — nothing to flag in this diff."
            : $"**{selection.TotalFindings} finding(s)**: {RenderTally(selection)}");
        AppendFoldedFindings(sb, selection.SummaryOnly);
        sb.AppendLine();
        sb.Append($"Full report: run `{runId}` in the agent-smith dashboard ")
          .Append($"(`.agentsmith/runs/{runId}/result.md`).");
        return sb.ToString();
    }

    private static string RenderFinding(SkillObservation observation)
    {
        var category = string.IsNullOrWhiteSpace(observation.Category)
            ? "" : $" · {observation.Category}";
        var suggestion = string.IsNullOrWhiteSpace(observation.Suggestion)
            ? "" : $"\n\n**Suggestion:** {observation.Suggestion}";
        return $"**{observation.Severity}**{category} — {observation.Description}{suggestion}";
    }

    private static string RenderTally(PrReviewFindingSelection selection)
    {
        var all = selection.Inline.SelectMany(g => g.Observations).Concat(selection.SummaryOnly);
        var counts = all.GroupBy(o => o.Severity).OrderBy(g => g.Key)
            .Select(g => $"{g.Key}: {g.Count()}");
        return string.Join(" · ", counts);
    }

    private static void AppendFoldedFindings(
        StringBuilder sb, IReadOnlyList<SkillObservation> folded)
    {
        if (folded.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine($"### Remaining {folded.Count} finding(s)");
        sb.AppendLine();
        sb.AppendLine("Not shown inline (render budget / no diff-line anchor) — full detail in result.md:");
        sb.AppendLine();
        foreach (var observation in folded)
            sb.AppendLine($"- `{observation.DisplayLocation}` — **{observation.Severity}** {FirstLine(observation.Description)}");
    }

    private static string FirstLine(string text)
    {
        var index = text.IndexOf('\n');
        return index < 0 ? text : text[..index].TrimEnd();
    }
}

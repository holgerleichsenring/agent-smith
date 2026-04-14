using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Decisions;
using AgentSmith.Contracts.Dialogue;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// Appends decision, dialogue, security-trend, and execution-trail
/// sections to a run-result markdown document.
/// </summary>
internal static class RunResultSectionWriter
{
    internal static void AppendDecisions(StringBuilder sb, IReadOnlyList<PlanDecision>? decisions)
    {
        if (decisions is null || decisions.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## Decisions");

        var grouped = decisions
            .GroupBy(d => d.Category, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in grouped)
        {
            sb.AppendLine();
            sb.AppendLine($"### {group.Key}");
            foreach (var d in group)
                sb.AppendLine($"- {d.Decision}");
        }
    }

    internal static void AppendDialogueTrail(StringBuilder sb, IReadOnlyList<DialogTrailEntry>? dialogueTrail)
    {
        if (dialogueTrail is null || dialogueTrail.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## Dialogue Trail");
        sb.AppendLine();
        sb.AppendLine("| Time | Question | Type | Answer | By | Timeout? |");
        sb.AppendLine("|------|----------|------|--------|-----|----------|");

        foreach (var entry in dialogueTrail)
        {
            var time = entry.Answer.AnsweredAt.ToString("HH:mm:ss");
            var question = entry.Question.Text.Length > 60
                ? entry.Question.Text[..60] + "..."
                : entry.Question.Text;
            var type = entry.Question.Type.ToString();
            var answer = entry.Answer.Answer.Length > 40
                ? entry.Answer.Answer[..40] + "..."
                : entry.Answer.Answer;
            var by = entry.Answer.AnsweredBy;
            var timedOut = entry.Answer.Comment == "timeout" ? "Yes" : "No";

            sb.AppendLine($"| {time} | {question} | {type} | {answer} | {by} | {timedOut} |");
        }
    }

    internal static void AppendSecurityTrend(StringBuilder sb, SecurityTrend? trend)
    {
        if (trend is null) return;

        sb.AppendLine();
        sb.AppendLine("## Security Trend");
        sb.AppendLine();
        sb.AppendLine("| Metric | Last Scan | This Scan | Delta |");
        sb.AppendLine("|--------|-----------|-----------|-------|");

        var prev = trend.Previous;
        AppendTrendRow(sb, "Critical", prev?.FindingsCritical, trend.Current.FindingsCritical, trend.CriticalDelta);
        AppendTrendRow(sb, "High", prev?.FindingsHigh, trend.Current.FindingsHigh, trend.HighDelta);
        AppendTrendRow(sb, "Medium", prev?.FindingsMedium, trend.Current.FindingsMedium,
            trend.Current.FindingsMedium - (prev?.FindingsMedium ?? 0));
        AppendTrendRow(sb, "Total", prev?.FindingsRetained, trend.Current.FindingsRetained,
            trend.Current.FindingsRetained - (prev?.FindingsRetained ?? 0));

        sb.AppendLine();
        sb.AppendLine($"**New findings:** {trend.NewFindings} | **Resolved:** {trend.ResolvedFindings} | **Scans:** {trend.TotalScans}");
    }

    private static void AppendTrendRow(
        StringBuilder sb, string metric, int? lastValue, int currentValue, int delta)
    {
        var last = lastValue.HasValue ? lastValue.Value.ToString() : "-";
        var deltaStr = delta > 0 ? $"+{delta}" : delta.ToString();
        sb.AppendLine($"| {metric} | {last} | {currentValue} | {deltaStr} |");
    }

    internal static void AppendExecutionTrail(StringBuilder sb, List<ExecutionTrailEntry>? trail)
    {
        if (trail is null || trail.Count == 0) return;

        sb.AppendLine();
        sb.AppendLine("## Execution Trail");
        sb.AppendLine();
        sb.AppendLine("| # | Command | Skill | Result | Duration | Inserted |");
        sb.AppendLine("|---|---------|-------|--------|----------|----------|");

        var totalDuration = TimeSpan.Zero;
        for (var i = 0; i < trail.Count; i++)
        {
            var e = trail[i];
            var status = e.Success ? "OK" : "FAIL";
            var msg = e.Message.Length > 40 ? e.Message[..40] + "..." : e.Message;
            var skill = e.Skill ?? "-";
            var duration = $"{e.Duration.TotalSeconds:F1}s";
            var inserted = e.InsertedCommandCount.HasValue ? $"+{e.InsertedCommandCount}" : "-";
            totalDuration += e.Duration;

            sb.AppendLine($"| {i + 1} | {e.CommandName} | {skill} | {status}: {msg} | {duration} | {inserted} |");
        }

        sb.AppendLine();
        sb.AppendLine($"**Total: {trail.Count} commands, {totalDuration.TotalSeconds:F1}s**");
    }
}

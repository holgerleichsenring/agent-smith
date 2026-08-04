using System.Text;
using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0397: persists a plan-call eval run as markdown + JSON, mirroring
/// <see cref="ExpectationEvalReportWriter"/>. The file name is DETERMINISTIC
/// per ticket source — re-running the same source overwrites in place so the
/// committed baseline carries history as diffs of one file.
/// </summary>
public static class PlanCallEvalReportWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Writes both artifacts; returns the markdown path.</summary>
    public static string Write(PlanCallEvalReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        var baseName = $"plan-call-eval-{Sanitize(report.TicketSource)}";
        File.WriteAllText(Path.Combine(directory, baseName + ".json"),
            JsonSerializer.Serialize(report, Json));
        var mdPath = Path.Combine(directory, baseName + ".md");
        File.WriteAllText(mdPath, RenderMarkdown(report));
        return mdPath;
    }

    private static string RenderMarkdown(PlanCallEvalReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Plan-call live eval");
        sb.AppendLine();
        sb.AppendLine($"- ticket source: `{report.TicketSource}`");
        sb.AppendLine($"- system prompt: {report.SystemPromptChars} chars, "
            + $"user prompt: {report.UserPromptChars} chars");
        sb.AppendLine($"- generated: {report.GeneratedAt:O}");
        sb.AppendLine();
        sb.AppendLine("| model | max out tokens | finish reason | out tokens | response chars "
            + "| parsed steps | salvaged steps | both repos | error |");
        sb.AppendLine("| --- | --- | --- | --- | --- | --- | --- | --- | --- |");
        foreach (var row in report.Rows)
            sb.AppendLine($"| {row.Model} | {row.MaxOutputTokens} | {row.FinishReason} "
                + $"| {row.OutputTokens?.ToString() ?? "-"} | {row.ResponseChars} "
                + $"| {row.ParsedSteps?.ToString() ?? "-"} | {row.SalvagedSteps?.ToString() ?? "-"} "
                + $"| {(row.BothReposCovered ? "yes" : "no")} | {row.Error ?? "-"} |");
        foreach (var row in report.Rows.Where(r => r.SampleTargets.Count > 0))
        {
            sb.AppendLine();
            sb.AppendLine($"## {row.Model} @ {row.MaxOutputTokens} — sample step targets");
            foreach (var target in row.SampleTargets)
                sb.AppendLine($"- {target}");
        }
        return sb.ToString();
    }

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }
}

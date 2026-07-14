using System.Text;
using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// p0329: persists an eval run as markdown + JSON under a reports directory.
/// The file name is DETERMINISTIC per model + skills pin — re-running the
/// same versions overwrites in place, so version-control history carries the
/// baseline and any drafting-prompt change shows up as a diff of the same
/// file, not a new anecdote next to it.
/// </summary>
public static class ExpectationEvalReportWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Writes both artifacts; returns the markdown path.</summary>
    public static string Write(ExpectationEvalReport report, string directory)
    {
        Directory.CreateDirectory(directory);
        var baseName = $"expectation-eval-{Sanitize(report.ModelId)}-{Sanitize(report.SkillsPin)}";
        File.WriteAllText(Path.Combine(directory, baseName + ".json"),
            JsonSerializer.Serialize(report, Json));
        var mdPath = Path.Combine(directory, baseName + ".md");
        File.WriteAllText(mdPath, RenderMarkdown(report));
        return mdPath;
    }

    private static string RenderMarkdown(ExpectationEvalReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Expectation replay eval");
        sb.AppendLine();
        sb.AppendLine($"- model: `{report.ModelId}`");
        sb.AppendLine($"- skills pin: `{report.SkillsPin}`");
        sb.AppendLine($"- generated: {report.GeneratedAt:O}");
        sb.AppendLine($"- fixtures: {report.Entries.Count}");
        sb.AppendLine();
        sb.AppendLine($"**Aggregate:** {report.Matched}/{report.TotalGold} gold assertions matched "
            + $"({report.MatchedRate:P0}), {report.Missed} missed, {report.Hallucinated} hallucinated.");
        foreach (var entry in report.Entries) RenderEntry(sb, entry);
        return sb.ToString();
    }

    private static void RenderEntry(StringBuilder sb, ExpectationEvalReport.FixtureEntry entry)
    {
        sb.AppendLine();
        sb.AppendLine($"## {entry.FixtureId}");
        if (entry.Verdict is null)
        {
            sb.AppendLine($"- DRAFT FAILED: {entry.DraftError}");
            sb.AppendLine($"- all {entry.GoldAssertions} gold assertions count as missed");
            return;
        }
        foreach (var gold in entry.Verdict.Gold)
            sb.AppendLine(gold.Matched
                ? $"- [x] matched: {gold.Assertion}\n  - by draft: {gold.MatchedDraftAssertion}"
                : $"- [ ] missed: {gold.Assertion}");
        foreach (var extra in entry.Verdict.Hallucinated)
            sb.AppendLine($"- hallucinated: {extra}");
    }

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }
}

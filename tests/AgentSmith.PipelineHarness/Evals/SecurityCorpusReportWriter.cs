using System.Text;
using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-28-cc40: persists a scoring run as markdown + JSON, keyed by MODEL and by the
/// scan master's own digest.
/// <para>
/// Keyed that way and not by the skills pin, for the reason
/// <see cref="AccountEvalReportWriter"/> gives: the whole value of a committed report is
/// that the next one is a diff of the SAME file, so the key must move when the thing under
/// test moves and not otherwise.
/// </para>
/// </summary>
public static class SecurityCorpusReportWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Writes both artifacts; returns the markdown path.</summary>
    public static string Write(SecurityCorpusReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(directory);
        var baseName =
            $"security-corpus-{Sanitize(report.ModelId)}-{Sanitize(report.ScanPromptVersion)}";
        File.WriteAllText(Path.Combine(directory, baseName + ".json"),
            JsonSerializer.Serialize(report, Json));
        var mdPath = Path.Combine(directory, baseName + ".md");
        File.WriteAllText(mdPath, RenderMarkdown(report));
        return mdPath;
    }

    public static string RenderMarkdown(SecurityCorpusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.AppendLine("# Security scan detection floor");
        sb.AppendLine();
        sb.AppendLine("> " + SecurityCorpusReport.CannotGradeSentence);
        sb.AppendLine();
        sb.AppendLine($"- model: `{report.ModelId}`");
        sb.AppendLine($"- scan master: `{report.ScanPromptVersion}`");
        sb.AppendLine($"- generated: {report.GeneratedAt:O}");
        sb.AppendLine();
        sb.AppendLine(
            $"**Misses:** {report.Misses}/{report.FlawedPopulation} ({report.MissRate:P0}) "
            + "— declared weaknesses no delivered finding named.");
        sb.AppendLine();
        sb.AppendLine(
            $"**False alarms:** {report.FalseAlarms}/{report.CleanPopulation} "
            + $"({report.FalseAlarmRate:P0}) — sound files a finding named anyway.");
        sb.AppendLine();
        sb.AppendLine(
            $"Cited line matched on {report.LineAccurateDetections} of {report.Detections} "
            + "detections — a citation sub-metric, not a gate.");
        RenderSilentSteps(sb, report);
        foreach (var entry in report.Entries) RenderEntry(sb, entry);
        return sb.ToString();
    }

    private static void RenderSilentSteps(StringBuilder sb, SecurityCorpusReport report)
    {
        if (report.StepsThatContributedNothing.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("**Contributed nothing to this score:** "
            + string.Join(", ", report.StepsThatContributedNothing)
            + " — a score is not a complete measurement of a scan whose steps stayed silent.");
    }

    private static void RenderEntry(StringBuilder sb, SecurityCorpusReport.CorpusEntry entry)
    {
        sb.AppendLine();
        sb.AppendLine($"## {entry.CorpusId}");
        if (entry.Problem is not null)
        {
            sb.AppendLine($"- SCAN NOT TAKEN: {entry.Problem}");
            return;
        }
        foreach (var file in entry.Files)
        {
            var mark = file.Agrees ? "[x]" : file.IsMiss ? "[MISS]" : "[FALSE ALARM]";
            sb.AppendLine($"- {mark} {file.Path} ({file.Class}, "
                + $"{(file.TruthIsFlawed ? "flawed" : "clean")})");
            if (file.FindingHeadline is not null)
                sb.AppendLine($"  - found [{file.HighestSeverity}]: {file.FindingHeadline}"
                    + (file.CitesDeclaredLine ? " (on the declared line)" : string.Empty));
        }
    }

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }
}

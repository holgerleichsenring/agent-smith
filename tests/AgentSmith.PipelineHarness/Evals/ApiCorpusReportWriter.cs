using System.Text;
using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-09-01-6686: persists an api scoring run as markdown + JSON, keyed by model and by
/// the api-security-master's own digest — the key
/// <see cref="SecurityCorpusReportWriter"/> uses, for the reason it gives.
/// </summary>
public static class ApiCorpusReportWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Writes both artifacts; returns the markdown path.</summary>
    public static string Write(ApiCorpusReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(directory);
        var baseName = $"api-corpus-{Sanitize(report.ModelId)}-{Sanitize(report.ScanPromptVersion)}";
        File.WriteAllText(Path.Combine(directory, baseName + ".json"),
            JsonSerializer.Serialize(report, Json));
        var mdPath = Path.Combine(directory, baseName + ".md");
        File.WriteAllText(mdPath, RenderMarkdown(report));
        return mdPath;
    }

    public static string RenderMarkdown(ApiCorpusReport report)
    {
        ArgumentNullException.ThrowIfNull(report);
        var sb = new StringBuilder();
        sb.AppendLine("# Api scan detection floor");
        sb.AppendLine();
        sb.AppendLine("> " + ApiCorpusReport.CannotGradeSentence);
        sb.AppendLine();
        sb.AppendLine($"- model: `{report.ModelId}`");
        sb.AppendLine($"- api scan master: `{report.ScanPromptVersion}`");
        sb.AppendLine($"- target: `{report.TargetId}`");
        sb.AppendLine($"- generated: {report.GeneratedAt:O}");
        sb.AppendLine();
        if (report.Problem is not null)
        {
            sb.AppendLine($"**SCAN NOT TAKEN:** {report.Problem}");
            sb.AppendLine();
        }
        sb.AppendLine(
            $"**Misses:** {report.Misses}/{report.WeakPopulation} ({report.MissRate:P0}) "
            + "— declared weaknesses no delivered finding named.");
        sb.AppendLine();
        sb.AppendLine(
            $"**False alarms:** {report.FalseAlarms}/{report.SoundPopulation} "
            + $"({report.FalseAlarmRate:P0}) — sound endpoints a finding named anyway.");
        RenderSilentSteps(sb, report);
        RenderUndeclared(sb, report);
        RenderEndpoints(sb, report);
        return sb.ToString();
    }

    private static void RenderSilentSteps(StringBuilder sb, ApiCorpusReport report)
    {
        sb.AppendLine();
        if (report.StepsThatContributedNothing.Count == 0)
        {
            sb.AppendLine("**Every step contributed.**");
            return;
        }
        sb.AppendLine("**Contributed nothing to this score:**");
        foreach (var step in report.StepsThatContributedNothing) sb.AppendLine($"- {step}");
        sb.AppendLine();
        sb.AppendLine("A score is not a complete measurement of a scan whose steps stayed silent.");
    }

    private static void RenderUndeclared(StringBuilder sb, ApiCorpusReport report)
    {
        if (report.UndeclaredLocations.Count == 0) return;
        sb.AppendLine();
        sb.AppendLine("**Named no declared endpoint (reported, not scored — no denominator):** "
            + string.Join(", ", report.UndeclaredLocations));
    }

    private static void RenderEndpoints(StringBuilder sb, ApiCorpusReport report)
    {
        sb.AppendLine();
        sb.AppendLine("## Endpoints");
        foreach (var endpoint in report.Endpoints)
        {
            var mark = endpoint.Agrees ? "[x]" : endpoint.IsMiss ? "[MISS]" : "[FALSE ALARM]";
            sb.AppendLine($"- {mark} `{endpoint.Endpoint}` ({endpoint.Class}, "
                + $"{(endpoint.TruthIsWeak ? "weak" : "sound")})");
            if (endpoint.FindingHeadline is not null)
                sb.AppendLine($"  - found [{endpoint.HighestSeverity}]: {endpoint.FindingHeadline}");
        }
    }

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }
}

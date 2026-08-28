using System.Text;
using AgentSmith.Domain.Models;
using System.Text.Json;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: persists a scoring run as markdown + JSON.
/// <para>
/// Keyed by model and by the ACCOUNT PROMPT's version, not by the skills pin. The account's
/// instructions live in this repository, so keying on a pin would name a version that does
/// not move when the thing under test does — and the whole value of a committed report is
/// that the next one is a diff of the same file.
/// </para>
/// </summary>
public static class AccountEvalReportWriter
{
    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    /// <summary>Writes both artifacts; returns the markdown path.</summary>
    public static string Write(AccountEvalReport report, string directory)
    {
        ArgumentNullException.ThrowIfNull(report);
        Directory.CreateDirectory(directory);
        var baseName = $"account-eval-{Sanitize(report.ModelId)}-{Sanitize(report.PromptVersion)}";
        File.WriteAllText(Path.Combine(directory, baseName + ".json"),
            JsonSerializer.Serialize(report, Json));
        var mdPath = Path.Combine(directory, baseName + ".md");
        File.WriteAllText(mdPath, RenderMarkdown(report));
        return mdPath;
    }

    private static string RenderMarkdown(AccountEvalReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Delivery account eval");
        sb.AppendLine();
        sb.AppendLine($"- model: `{report.ModelId}`");
        sb.AppendLine($"- account prompt: `{report.PromptVersion}`");
        sb.AppendLine($"- generated: {report.GeneratedAt:O}");
        sb.AppendLine($"- fixtures: {report.Entries.Count}");
        sb.AppendLine($"- classes covered: {string.Join(", ", report.ClassesCovered)}");
        sb.AppendLine();
        sb.AppendLine(
            $"**False negatives:** {report.FalseNegatives}/{report.MetPopulation} "
            + $"({report.FalseNegativeRate:P0}) — met criteria the account refused.");
        sb.AppendLine();
        sb.AppendLine(
            $"**False positives:** {report.FalsePositives}/{report.UnmetPopulation} "
            + $"({report.FalsePositiveRate:P0}) — unmet criteria the account passed.");
        foreach (var entry in report.Entries) RenderEntry(sb, entry);
        return sb.ToString();
    }

    private static void RenderEntry(StringBuilder sb, AccountEvalReport.FixtureEntry entry)
    {
        sb.AppendLine();
        sb.AppendLine($"## {entry.FixtureId} ({entry.Class})");
        if (entry.Problem is not null)
        {
            sb.AppendLine($"- ACCOUNT NOT TAKEN: {entry.Problem}");
            return;
        }
        foreach (var outcome in entry.Criteria)
        {
            var mark = outcome.Agrees ? "[x]" : outcome.IsFalseNegative ? "[FN]" : "[FP]";
            sb.AppendLine($"- {mark} truth={(outcome.TruthIsMet ? "met" : "unmet")}, "
                + $"account={Disposition(outcome.AccountDisposition)}: "
                + outcome.Criterion);
            if (outcome.Citation is not null) sb.AppendLine($"  - cited: {outcome.Citation}");
            if (outcome.Note is not null) sb.AppendLine($"  - note: {outcome.Note}");
        }
    }

    /// <summary>2026-08-25-9749: the report prints the disposition, not a bool. A declined
    /// criterion reading as "not satisfied" would hide the very answer this corpus exists to
    /// let a human label.</summary>
    private static string Disposition(AccountDisposition disposition) => disposition switch
    {
        AccountDisposition.Satisfied => "satisfied",
        AccountDisposition.NotApplicable => "not applicable",
        _ => "not satisfied",
    };

    private static string Sanitize(string value)
    {
        var cleaned = new string(value.Select(c =>
            char.IsLetterOrDigit(c) || c is '-' or '.' ? c : '-').ToArray()).Trim('-');
        return cleaned.Length == 0 ? "unknown" : cleaned;
    }
}

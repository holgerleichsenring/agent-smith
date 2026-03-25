using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Clean findings-only summary output. No skill discussion, no round-by-round noise.
/// Groups retained findings by severity with cost line. Always stdout.
/// </summary>
public sealed partial class SummaryOutputStrategy(
    ILogger<SummaryOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "summary";

    public Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        var consolidated = GetConsolidatedOutput(context);

        if (string.IsNullOrWhiteSpace(consolidated))
        {
            Console.WriteLine("No findings to summarize.");
            return Task.CompletedTask;
        }

        var findings = ParseFindings(consolidated);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine("  Agent Smith — API Security Summary");
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine();

        var grouped = findings
            .GroupBy(f => f.Severity)
            .OrderBy(g => SeverityOrder(g.Key));

        foreach (var group in grouped)
        {
            sb.AppendLine($"{group.Key.ToUpperInvariant()} ({group.Count()})");
            foreach (var f in group)
                sb.AppendLine($"  {f.Title}{(f.Confidence > 0 ? $" — confidence {f.Confidence}" : "")}");
            sb.AppendLine();
        }

        sb.AppendLine($"Total: {findings.Count} findings");

        if (context.Pipeline.TryGet<object>("PipelineCostTracker", out var tracker))
            sb.AppendLine($"{tracker}");

        sb.AppendLine("═══════════════════════════════════════");

        Console.Write(sb.ToString());

        logger.LogInformation("Summary delivered ({Count} findings)", findings.Count);
        return Task.CompletedTask;
    }

    private static string? GetConsolidatedOutput(OutputContext context)
    {
        if (context.ReportMarkdown is not null)
            return context.ReportMarkdown;

        context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated);
        return consolidated;
    }

    internal static List<SummaryFinding> ParseFindings(string text)
    {
        var findings = new List<SummaryFinding>();

        // Match patterns like: **1. Title** or numbered items with severity
        // Also match: - severity: HIGH/MEDIUM/LOW followed by title lines
        var lines = text.Split('\n');

        string? currentTitle = null;
        string sectionSeverity = "MEDIUM";
        string? findingSeverity = null;
        int currentConfidence = 0;

        foreach (var line in lines)
        {
            var trimmed = line.Trim();

            // Match "## Critical Issues" or "## HIGH Severity" section headers — sets default for following findings
            var sectionMatch = SeveritySectionRegex().Match(trimmed);
            if (sectionMatch.Success)
            {
                // Flush pending finding before changing section
                if (currentTitle is not null)
                {
                    findings.Add(new SummaryFinding(currentTitle, findingSeverity ?? sectionSeverity, currentConfidence));
                    currentTitle = null;
                    findingSeverity = null;
                }

                var sv = sectionMatch.Groups[1].Value.ToUpperInvariant();
                if (sv is "CRITICAL") sectionSeverity = "CRITICAL";
                else if (sv.Contains("HIGH")) sectionSeverity = "HIGH";
                else if (sv.Contains("MEDIUM")) sectionSeverity = "MEDIUM";
                else if (sv.Contains("LOW")) sectionSeverity = "LOW";
                continue;
            }

            // Match "**N. Title**" pattern
            var numberedMatch = NumberedFindingRegex().Match(trimmed);
            if (numberedMatch.Success)
            {
                if (currentTitle is not null)
                    findings.Add(new SummaryFinding(currentTitle, findingSeverity ?? sectionSeverity, currentConfidence));

                currentTitle = numberedMatch.Groups[1].Value.Trim();
                findingSeverity = null;
                currentConfidence = 0;
                continue;
            }

            // Match "- severity: HIGH" pattern — overrides section header for this finding
            var severityMatch = SeverityLineRegex().Match(trimmed);
            if (severityMatch.Success && currentTitle is not null)
            {
                findingSeverity = severityMatch.Groups[1].Value.ToUpperInvariant();
                continue;
            }

            // Match "- confidence: 9" pattern
            var confidenceMatch = ConfidenceLineRegex().Match(trimmed);
            if (confidenceMatch.Success && currentTitle is not null)
            {
                if (int.TryParse(confidenceMatch.Groups[1].Value, out var conf))
                    currentConfidence = conf;
                continue;
            }
        }

        if (currentTitle is not null)
            findings.Add(new SummaryFinding(currentTitle, findingSeverity ?? sectionSeverity, currentConfidence));

        return findings;
    }

    private static int SeverityOrder(string severity) => severity.ToUpperInvariant() switch
    {
        "CRITICAL" => 0,
        "HIGH" => 1,
        "MEDIUM" => 2,
        "LOW" => 3,
        _ => 4,
    };

    [GeneratedRegex(@"\*\*\d+\.\s+(.+?)\*\*")]
    private static partial Regex NumberedFindingRegex();

    [GeneratedRegex(@"^-\s*severity:\s*(HIGH|MEDIUM|LOW|CRITICAL)", RegexOptions.IgnoreCase)]
    private static partial Regex SeverityLineRegex();

    [GeneratedRegex(@"^-\s*confidence:\s*(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ConfidenceLineRegex();

    [GeneratedRegex(@"^##\s*(Critical|High|Medium|Low)", RegexOptions.IgnoreCase)]
    private static partial Regex SeveritySectionRegex();

    internal sealed record SummaryFinding(string Title, string Severity, int Confidence);
}

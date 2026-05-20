using System.Text;
using System.Text.RegularExpressions;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Clean findings-only summary output. No skill discussion, no round-by-round noise.
/// Groups retained findings by severity with cost line. Always stdout.
/// p0151h: appends an anchoring-verification block so source-anchor / orphan
/// regressions surface in the operator-facing summary.
/// </summary>
public sealed partial class SummaryOutputStrategy(
    AnchoringVerifier anchoringVerifier,
    ILogger<SummaryOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "summary";

    public Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        // p0147b: split runtime execution-limit / execution-error observations
        // out of the findings list — they render in their own footer section
        // so silent skill drops are visible without polluting the severity tally.
        var (operatorObs, limitObs) = SplitByCategory(context.Observations);
        var findings = operatorObs.Count > 0
            ? FromObservations(operatorObs)
            : ParseFromMarkdown(context);

        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine("  Agent Smith — API Security Summary");
        sb.AppendLine("═══════════════════════════════════════");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("No findings.");
            sb.AppendLine();
        }
        else
        {
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
        }

        AppendEvidenceBreakdown(sb, operatorObs);

        sb.AppendLine($"Total: {findings.Count} findings");

        if (limitObs.Count > 0)
        {
            sb.AppendLine($"Execution limits hit: {limitObs.Count}");
            foreach (var obs in limitObs)
                sb.AppendLine($"  [{LimitLabel(obs.Category)}] {ExtractTitle(obs.Description)}");
        }

        AppendVerification(sb, operatorObs);

        if (context.Pipeline.TryGet<object>("PipelineCostTracker", out var tracker))
            sb.AppendLine($"{tracker}");

        sb.AppendLine("═══════════════════════════════════════");

        Console.Write(sb.ToString());

        logger.LogInformation(
            "Summary delivered ({Count} findings, {Limits} limit hits)",
            findings.Count, limitObs.Count);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Surfaces evidence-mode breakdown — how many findings are code-grounded
    /// (analyzed_from_source) vs HTTP-probe-confirmed vs schema/inferred. Lists
    /// the source-anchored findings inline with file:line so operators can tell
    /// at a glance whether the run produced real code analysis or stayed at
    /// schema-lint level.
    /// </summary>
    private static void AppendEvidenceBreakdown(StringBuilder sb, IReadOnlyList<SkillObservation> operatorObservations)
    {
        if (operatorObservations.Count == 0) return;
        var sourceAnchored = operatorObservations
            .Where(o => o.EvidenceMode == EvidenceMode.AnalyzedFromSource)
            .ToList();
        var confirmed = operatorObservations.Count(o => o.EvidenceMode == EvidenceMode.Confirmed);
        var schema = operatorObservations.Count - sourceAnchored.Count - confirmed;

        if (sourceAnchored.Count == 0)
        {
            sb.AppendLine($"Evidence: 0 from source, {confirmed} confirmed, {schema} schema/inferred");
            sb.AppendLine();
            return;
        }

        sb.AppendLine($"Code Findings ({sourceAnchored.Count})");
        foreach (var obs in sourceAnchored.OrderBy(o => SeverityOrder(o.Severity.ToString())))
            sb.AppendLine(
                $"  [{obs.Severity.ToString().ToUpperInvariant()}] {obs.DisplayLocation} — {ExtractTitle(obs.Description)}");
        sb.AppendLine();
        sb.AppendLine($"Evidence: {sourceAnchored.Count} from source, {confirmed} confirmed, {schema} schema/inferred");
        sb.AppendLine();
    }

    private void AppendVerification(StringBuilder sb, IReadOnlyList<SkillObservation> operatorObservations)
    {
        var assertions = anchoringVerifier.Verify(operatorObservations);
        if (assertions.Count == 0) return;
        var passed = assertions.Count(a => a.Passed);
        sb.AppendLine($"Verification: {passed}/{assertions.Count} passed");
        foreach (var assertion in assertions)
        {
            var marker = assertion.Passed ? "PASS" : "FAIL";
            sb.AppendLine($"  [{marker}] {assertion.Name} — {assertion.Detail}");
        }
    }

    private static (List<SkillObservation> Operator, List<SkillObservation> Limits)
        SplitByCategory(IReadOnlyList<SkillObservation> observations)
    {
        var op = new List<SkillObservation>(observations.Count);
        var limits = new List<SkillObservation>();
        foreach (var obs in observations)
        {
            if (ExecutionLimitCategories.IsExecutionLimit(obs.Category))
                limits.Add(obs);
            else
                op.Add(obs);
        }
        return (op, limits);
    }

    private static string LimitLabel(string? category) => category switch
    {
        ExecutionLimitCategories.ExecutionLimitToolCalls => "tool-call limit",
        ExecutionLimitCategories.ExecutionLimitTokens => "token limit",
        ExecutionLimitCategories.ExecutionLimitWallClock => "wall-clock limit",
        ExecutionLimitCategories.ExecutionError => "runtime error",
        ExecutionLimitCategories.CostCapExhausted => "cost cap",
        ExecutionLimitCategories.ExecutionParseFailure => "parse failure",
        _ => "execution limit"
    };

    private static List<SummaryFinding> FromObservations(IReadOnlyList<SkillObservation> observations) =>
        observations.Select(o => new SummaryFinding(
            ExtractTitle(o.Description),
            o.Severity.ToString().ToUpperInvariant(),
            o.Confidence)).ToList();

    private static string ExtractTitle(string description)
    {
        var firstLine = description.Split('\n')[0].Trim();
        return firstLine.Length > 80 ? firstLine[..80] + "…" : firstLine;
    }

    private static List<SummaryFinding> ParseFromMarkdown(OutputContext context)
    {
        var consolidated = GetConsolidatedOutput(context);
        return string.IsNullOrWhiteSpace(consolidated)
            ? new List<SummaryFinding>()
            : ParseFindings(consolidated);
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

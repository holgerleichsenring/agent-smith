using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Writes findings summary to stdout. Default strategy for --output console.
/// Handles both structured findings and free-text consolidated output.
/// </summary>
public sealed class ConsoleOutputStrategy(
    ILogger<ConsoleOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "console";

    public Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        var output = BuildOutput(context);

        if (string.IsNullOrWhiteSpace(output))
        {
            Console.WriteLine("No findings to deliver.");
            return Task.CompletedTask;
        }

        Console.WriteLine();
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine("  Agent Smith Review");
        Console.WriteLine("═══════════════════════════════════════════════════");
        Console.WriteLine();
        Console.WriteLine(output);
        Console.WriteLine();

        if (context.Pipeline.TryGet<object>("PipelineCostTracker", out var tracker))
            Console.WriteLine($"  {tracker}");

        Console.WriteLine("═══════════════════════════════════════════════════");

        logger.LogInformation("Delivered to console ({Chars} chars)", output.Length);
        return Task.CompletedTask;
    }

    private static string? BuildOutput(OutputContext context)
    {
        if (context.ReportMarkdown is not null)
            return context.ReportMarkdown;

        if (context.Findings.Count > 0)
            return FormatFindings(context);

        context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated);
        if (!string.IsNullOrWhiteSpace(consolidated))
            return consolidated;

        // DiscussionLog is compiled into ConsolidatedPlan by ConvergenceCheckHandler.
        // If neither exists, there's nothing to show.

        return null;
    }

    private static string FormatFindings(OutputContext context)
    {
        var summary = FindingSummary.From(context.Findings);
        var reviewInfo = summary.Confirmed > 0 || summary.NotReviewed < summary.Total
            ? $" — {summary.Confirmed} confirmed, {summary.NotReviewed} not reviewed"
            : "";
        var lines = new List<string>
        {
            $"Found {summary.Total} issues ({summary.Critical} CRITICAL, {summary.High} HIGH, {summary.Medium} MEDIUM, {summary.Low} LOW){reviewInfo}",
            ""
        };

        foreach (var finding in context.Findings)
        {
            var status = finding.ReviewStatus == "confirmed" ? " ✓" : "";
            var badge = EvidenceBadge(finding.EvidenceMode);
            lines.Add($"[{finding.Severity.ToUpperInvariant()}]{badge} {finding.DisplayLocation} — {finding.Title}{status}");
        }

        return string.Join("\n", lines);
    }

    private static string EvidenceBadge(EvidenceMode mode) => mode switch
    {
        EvidenceMode.Confirmed => " [confirmed]",
        EvidenceMode.AnalyzedFromSource => " [source]",
        _ => ""
    };
}

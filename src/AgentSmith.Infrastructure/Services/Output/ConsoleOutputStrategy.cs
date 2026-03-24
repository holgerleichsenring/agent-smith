using AgentSmith.Contracts.Commands;
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
        var lines = new List<string>
        {
            $"Found {summary.Total} issues ({summary.High} HIGH, {summary.Medium} MEDIUM, {summary.Low} LOW)",
            ""
        };

        foreach (var finding in context.Findings)
        {
            lines.Add($"[{finding.Severity.ToUpperInvariant()}] {finding.File}:{finding.StartLine} — {finding.Title}");
        }

        return string.Join("\n", lines);
    }
}

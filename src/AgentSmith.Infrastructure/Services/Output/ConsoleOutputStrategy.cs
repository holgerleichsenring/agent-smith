using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Contracts.Models.Configuration;
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

        if (context.Observations.Count > 0)
            return FormatObservations(context);

        context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated);
        if (string.IsNullOrWhiteSpace(consolidated))
            return null;

        // Structured pipelines (security-scan, api-security-scan) deliver findings as their
        // primary output; the consolidated discussion is supplementary and belongs in the file
        // outputs, not on the console. Discussion pipelines (mad-discussion, legal-analysis)
        // keep dumping the consolidated plan because the discussion IS the deliverable.
        if (IsStructured(context.Pipeline))
            return $"Discussion compiled ({consolidated.Length} chars) — see file output for full report.";

        return consolidated;
    }

    private static bool IsStructured(PipelineContext pipeline) =>
        pipeline.TryGet<PipelineType>(ContextKeys.PipelineTypeName, out var type)
        && type == PipelineType.Structured;

    private static string FormatObservations(OutputContext context)
    {
        var summary = ObservationSummary.From(context.Observations);
        var reviewInfo = summary.Confirmed > 0 || summary.NotReviewed < summary.Total
            ? $" — {summary.Confirmed} confirmed, {summary.NotReviewed} not reviewed"
            : "";
        var lines = new List<string>
        {
            $"Found {summary.Total} issues ({summary.High} HIGH, {summary.Medium} MEDIUM, {summary.Low} LOW, {summary.Info} INFO){reviewInfo}",
            ""
        };

        foreach (var obs in context.Observations)
        {
            var status = obs.ReviewStatus == "confirmed" ? " ✓" : "";
            var badge = EvidenceBadge(obs.EvidenceMode);
            var title = ExtractTitle(obs.Description);
            lines.Add($"[{obs.Severity.ToString().ToUpperInvariant()}]{badge} {obs.DisplayLocation} — {title}{status}");
        }

        return string.Join("\n", lines);
    }

    private static string ExtractTitle(string description)
    {
        var firstLine = description.Split('\n')[0].Trim();
        return firstLine.Length > 80 ? firstLine[..80] + "…" : firstLine;
    }

    private static string EvidenceBadge(EvidenceMode mode) => mode switch
    {
        EvidenceMode.Confirmed => " [confirmed]",
        EvidenceMode.AnalyzedFromSource => " [source]",
        _ => ""
    };
}

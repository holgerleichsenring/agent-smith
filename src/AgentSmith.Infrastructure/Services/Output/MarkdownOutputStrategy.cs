using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Renders findings as a Markdown report. Writes to OutputDir/findings.md.
/// </summary>
public sealed class MarkdownOutputStrategy(
    ILogger<MarkdownOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "markdown";

    public async Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        var markdown = context.Findings.Count > 0
            ? BuildMarkdown(context.Findings)
            : BuildFromPipeline(context);

        Directory.CreateDirectory(context.OutputDir);
        var outputPath = Path.Combine(context.OutputDir, "findings.md");
        await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
        logger.LogInformation("Markdown report written to {Path}", outputPath);
    }

    private static string BuildFromPipeline(OutputContext context)
    {
        if (context.ReportMarkdown is not null)
            return context.ReportMarkdown;

        context.Pipeline.TryGet<string>(ContextKeys.ConsolidatedPlan, out var consolidated);
        return consolidated ?? "No findings to report.";
    }

    internal static string BuildMarkdown(IReadOnlyList<Finding> findings)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Agent Smith Security Review");
        sb.AppendLine();

        if (findings.Count == 0)
        {
            sb.AppendLine("No issues found.");
            return sb.ToString();
        }

        var s = FindingSummary.From(findings);

        sb.AppendLine($"Found {s.Total} issues ({s.High} HIGH, {s.Medium} MEDIUM, {s.Low} LOW)");
        sb.AppendLine();

        sb.AppendLine("| Severity | Location | Issue |");
        sb.AppendLine("|----------|----------|-------|");

        foreach (var f in findings)
        {
            var icon = f.Severity.ToUpperInvariant() switch
            {
                "HIGH" => "\ud83d\udd34 HIGH",
                "MEDIUM" => "\ud83d\udfe1 MEDIUM",
                "LOW" => "\ud83d\udfe2 LOW",
                _ => f.Severity
            };

            sb.AppendLine($"| {icon} | {f.DisplayLocation} | {f.Title} |");
        }

        return sb.ToString();
    }
}

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

        sb.AppendLine($"Found **{s.Total}** issues ({s.Critical} critical, {s.High} high, {s.Medium} medium, {s.Low} low)");
        sb.AppendLine();

        sb.AppendLine("| Severity | Location | Issue | Details |");
        sb.AppendLine("|----------|----------|-------|---------|");

        foreach (var f in findings)
        {
            var icon = f.Severity.ToUpperInvariant() switch
            {
                "CRITICAL" => "\ud83d\udd34 CRITICAL",
                "HIGH" => "\ud83d\udfe0 HIGH",
                "MEDIUM" => "\ud83d\udfe1 MEDIUM",
                "LOW" => "\ud83d\udfe2 LOW",
                _ => f.Severity
            };

            var location = EscapePipe(f.DisplayLocation);
            var title = EscapePipe(Truncate(f.Title, 80));
            var details = EscapePipe(Truncate(f.Description, 120));

            sb.AppendLine($"| {icon} | `{location}` | {title} | {details} |");
        }

        return sb.ToString();
    }

    private static string EscapePipe(string text) =>
        text.Replace("|", "\\|");

    private static string Truncate(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength] + "...";
}

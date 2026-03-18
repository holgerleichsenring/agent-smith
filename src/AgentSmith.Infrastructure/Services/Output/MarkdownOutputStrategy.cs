using System.Text;
using AgentSmith.Contracts.Services;
using Microsoft.Extensions.Logging;

namespace AgentSmith.Infrastructure.Services.Output;

/// <summary>
/// Renders findings as a Markdown table. Used for --output markdown
/// and as the PR/MR comment body.
/// </summary>
public sealed class MarkdownOutputStrategy(
    ILogger<MarkdownOutputStrategy> logger) : IOutputStrategy
{
    public string ProviderType => "markdown";

    public async Task DeliverAsync(OutputContext context, CancellationToken cancellationToken = default)
    {
        var markdown = BuildMarkdown(context.Findings);

        var outputPath = Path.Combine(Directory.GetCurrentDirectory(), "findings.md");
        await File.WriteAllTextAsync(outputPath, markdown, cancellationToken);
        logger.LogInformation("Markdown report written to {Path}", outputPath);

        logger.LogInformation("{Report}", markdown);
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

        sb.AppendLine("| Severity | File | Line | Issue |");
        sb.AppendLine("|----------|------|------|-------|");

        foreach (var f in findings)
        {
            var icon = f.Severity.ToUpperInvariant() switch
            {
                "HIGH" => "\ud83d\udd34 HIGH",
                "MEDIUM" => "\ud83d\udfe1 MEDIUM",
                "LOW" => "\ud83d\udfe2 LOW",
                _ => f.Severity
            };

            sb.AppendLine($"| {icon} | {f.File} | {f.StartLine} | {f.Title} |");
        }

        return sb.ToString();
    }
}

using System.Text;
using AgentSmith.Contracts.Commands;
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
        // Use consolidated plan/discussion as markdown if no structured findings
        var markdown = context.Findings.Count > 0
            ? BuildMarkdown(context.Findings)
            : BuildFromPipeline(context);

        var outputDir = ResolveOutputDir(context);
        Directory.CreateDirectory(outputDir);

        var outputPath = Path.Combine(outputDir, "findings.md");
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

    private static string ResolveOutputDir(OutputContext context)
    {
        context.Pipeline.TryGet<string>(ContextKeys.OutputDir, out var dir);
        if (!string.IsNullOrWhiteSpace(dir) && IsWritable(dir))
            return dir;

        if (IsWritable("/output"))
            return "/output";

        return "./agentsmith-output";
    }

    private static bool IsWritable(string path)
    {
        try
        {
            Directory.CreateDirectory(path);
            var testFile = Path.Combine(path, ".write-test");
            File.WriteAllText(testFile, "");
            File.Delete(testFile);
            return true;
        }
        catch { return false; }
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

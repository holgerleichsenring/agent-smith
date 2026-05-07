using System.Text;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
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
        var markdown = context.Observations.Count > 0
            ? BuildMarkdown(context.Observations)
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

    internal static string BuildMarkdown(IReadOnlyList<SkillObservation> observations)
    {
        var sb = new StringBuilder();
        sb.AppendLine("## Agent Smith Security Review");
        sb.AppendLine();

        if (observations.Count == 0)
        {
            sb.AppendLine("No issues found.");
            return sb.ToString();
        }

        var s = ObservationSummary.From(observations);

        sb.AppendLine($"Found **{s.Total}** issues ({s.High} high, {s.Medium} medium, {s.Low} low, {s.Info} info)");
        sb.AppendLine();

        foreach (var o in observations)
        {
            var icon = o.Severity switch
            {
                ObservationSeverity.High => "\ud83d\udfe0",
                ObservationSeverity.Medium => "\ud83d\udfe1",
                ObservationSeverity.Low => "\ud83d\udfe2",
                _ => "\u2022"
            };

            var title = ExtractTitle(o.Description);
            sb.AppendLine($"### {icon} {o.Severity.ToString().ToUpperInvariant()}: {title}");
            sb.AppendLine();
            sb.AppendLine($"**Location:** `{o.DisplayLocation}`  ");
            sb.AppendLine($"**Evidence:** {EvidenceLabel(o.EvidenceMode)}");
            sb.AppendLine();
            sb.AppendLine(o.Description);
            if (!string.IsNullOrWhiteSpace(o.Suggestion))
            {
                sb.AppendLine();
                sb.AppendLine($"**Suggestion:** {o.Suggestion}");
            }
            sb.AppendLine();
        }

        return sb.ToString();
    }

    private static string ExtractTitle(string description)
    {
        var firstLine = description.Split('\n')[0].Trim();
        return firstLine.Length > 80 ? firstLine[..80] + "\u2026" : firstLine;
    }

    private static string EvidenceLabel(EvidenceMode mode) => mode switch
    {
        EvidenceMode.Confirmed => "confirmed (HTTP probe)",
        EvidenceMode.AnalyzedFromSource => "analyzed from source",
        _ => "potential (schema/pattern)"
    };
}

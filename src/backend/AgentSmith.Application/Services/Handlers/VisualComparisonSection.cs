using System.Text;
using AgentSmith.Application.Services.Browser;
using AgentSmith.Contracts.Commands;

namespace AgentSmith.Application.Services.Handlers;

/// <summary>
/// 2026-10-01-283di: the run's compare_reference calls in result.md — each comparison's style
/// differences and pixel similarity per viewport, and the diff images under the run record. The
/// operator ruled that differences report and never gate, so nothing here reaches the delivery
/// gate and the section says so. Empty when the run compared nothing.
/// </summary>
public static class VisualComparisonSection
{
    public const string Heading = "## Visual comparison";

    public static string Build(PipelineContext pipeline)
    {
        ArgumentNullException.ThrowIfNull(pipeline);
        var comparisons = VisualComparisonRecorder.Recorded(pipeline);
        if (comparisons.Count == 0) return string.Empty;
        var text = new StringBuilder("\n\n").Append(Heading).Append("\n\n")
            .Append("Reported, never gated: neither the style differences nor the pixel similarity changes this run's outcome.\n");
        for (var i = 0; i < comparisons.Count; i++)
        {
            text.Append($"\n### {i + 1}. `{comparisons[i].Candidate}` against `{comparisons[i].Reference}`\n\n");
            VisualComparisonLines.Append(text, comparisons[i]);
        }
        return text.ToString().TrimEnd();
    }
}

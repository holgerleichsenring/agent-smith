using System.Globalization;
using System.Text;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: a comparison in words — the same lines for the model's tool result and for the
/// run's result.md, so the two never disagree about what differed.
/// </summary>
public static class VisualComparisonLines
{
    internal const int MaxDifferences = 60;

    public static void Append(StringBuilder text, VisualComparison comparison)
    {
        ArgumentNullException.ThrowIfNull(text);
        ArgumentNullException.ThrowIfNull(comparison);
        foreach (var view in comparison.Viewports)
        {
            text.Append(CultureInfo.InvariantCulture,
                $"- {view.Viewport}: {view.MismatchRatio * 100:0.00}% of pixels differ (reference {view.ReferenceHeight} px, "
                + $"candidate {view.CandidateHeight} px; the shorter padded with magenta to {view.PaddedHeight} px)\n");
            text.Append($"  Style differences ({view.Differences.Count}):\n");
            if (view.Differences.Count == 0) text.Append("  - none: every paired property computed the same\n");
            foreach (var d in view.Differences.Take(MaxDifferences))
                text.Append($"  - {d.ReferenceSelector} ⇄ {d.CandidateSelector}: {d.Property} — reference {d.ReferenceValue}, candidate {d.CandidateValue}\n");
            if (view.Differences.Count > MaxDifferences)
                text.Append($"  - … and {view.Differences.Count - MaxDifferences} more\n");
        }
        foreach (var image in comparison.Images) text.Append($"- diff image: `{image}`\n");
    }
}

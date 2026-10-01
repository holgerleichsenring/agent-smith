using System.Text;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283di: the text half of a comparison — what was loaded, each viewport's style
/// differences and pixel similarity in the words result.md uses too, and what went wrong on the way.
/// </summary>
public static class CompareResultText
{
    public static string Render(BrowserRenderResult result, VisualComparison comparison, IReadOnlyList<string> notes)
    {
        ArgumentNullException.ThrowIfNull(result);
        var report = result.Compare;
        var text = new StringBuilder($"compare_reference: reference {report?.ReferenceUrl} against candidate {report?.CandidateUrl}\n");
        text.Append("A report, not a verdict: nothing here passes or fails the work.\n\n");
        VisualComparisonLines.Append(text, comparison);
        Listed(text, "Console errors", result.ConsoleErrors);
        Listed(text, "Failed requests", [.. result.FailedRequests.Select(n => $"{n.Url} — {n.Reason}")]);
        Listed(text, "Requests the egress guard refused", [.. result.Refused.Select(n => $"{n.Url} — {n.Reason}")]);
        Listed(text, "Images and copies", notes);
        return text.ToString();
    }

    private static void Listed(StringBuilder text, string heading, IReadOnlyList<string> lines)
    {
        text.Append($"\n{heading} ({lines.Count}):\n");
        foreach (var line in lines.Take(RenderResultText.MaxListed)) text.Append("- ").Append(line).Append('\n');
        if (lines.Count > RenderResultText.MaxListed) text.Append($"- … and {lines.Count - RenderResultText.MaxListed} more\n");
    }
}

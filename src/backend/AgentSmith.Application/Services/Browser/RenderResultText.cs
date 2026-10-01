using System.Text;

namespace AgentSmith.Application.Services.Browser;

/// <summary>
/// 2026-10-01-283de: the text half of a render — the exact half. Computed styles are listed as the
/// browser computed them, so the model quotes a colour instead of estimating it from a picture; what
/// failed and what the egress guard refused is listed too, so a page that rendered without its
/// stylesheet is not mistaken for the page.
/// </summary>
public sealed class RenderResultText
{
    internal const int MaxListed = 50;
    private const int MaxLine = 240;

    public string Render(BrowserRenderResult result, IReadOnlyList<string> shotNotes)
    {
        ArgumentNullException.ThrowIfNull(result);
        var text = new StringBuilder($"render_reference: {result.Url} — \"{result.Title}\"\n");
        if (result.Error is { } error) text.Append($"The page did not render: {Clip(error)}\n");
        text.Append("\nComputed styles (desktop viewport, first match of each selector):\n");
        foreach (var row in result.Styles) AppendRow(text, row);
        AppendList(text, "Console errors", result.ConsoleErrors);
        AppendList(text, "Failed requests", result.FailedRequests.Select(n => $"{n.Url} — {n.Reason}").ToList());
        AppendList(text, "Requests the egress guard refused", result.Refused.Select(n => $"{n.Url} — {n.Reason}").ToList());
        text.Append("\nScreenshots:\n");
        foreach (var note in shotNotes) text.Append("- ").Append(note).Append('\n');
        return text.ToString();
    }

    private static void AppendRow(StringBuilder text, BrowserStyleRow row)
    {
        if (row.Count == 0 || row.Values is null)
        {
            text.Append($"{row.Selector}: no match\n");
            return;
        }
        text.Append($"{row.Selector} ({row.Count} match{(row.Count == 1 ? "" : "es")}):\n");
        foreach (var (property, value) in row.Values) text.Append($"  {property}: {Clip(value)}\n");
    }

    private static void AppendList(StringBuilder text, string heading, IReadOnlyList<string> lines)
    {
        text.Append($"\n{heading} ({lines.Count}):\n");
        if (lines.Count == 0) text.Append("- none\n");
        foreach (var line in lines.Take(MaxListed)) text.Append("- ").Append(Clip(line)).Append('\n');
        if (lines.Count > MaxListed) text.Append($"- … and {lines.Count - MaxListed} more\n");
    }

    private static string Clip(string value) =>
        value.Length <= MaxLine ? value : value[..MaxLine] + "…";
}

using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eb: the acceptance criteria a ticket carries in its BODY, under their own
/// heading — how a requirement ticket states when it is done on every tracker, since only
/// Azure DevOps maps an acceptance field. Read in both encodings a body arrives in: markdown,
/// and the HTML Azure DevOps stores a markdown description as.
/// <para>
/// Only list items count. A person's ticket carries prose, form placeholders, comments and code
/// under the same heading, and none of that is a criterion the run can be held to.
/// </para>
/// </summary>
public static partial class AcceptanceCriteriaSection
{
    public const string Heading = "## Acceptance criteria";

    /// <summary>The section's list items, one criterion each; empty when the body has none.</summary>
    public static IReadOnlyList<string> Read(string? body)
    {
        if (string.IsNullOrWhiteSpace(body)) return [];
        var html = HtmlSection().Match(body);
        var items = html.Success ? HtmlItems(html.Groups["section"].Value) : MarkdownItems(body);
        return [.. items.Where(item => !CriterionLine.IsPlaceholder(item))];
    }

    private static IEnumerable<string> HtmlItems(string section) =>
        ListItem().Matches(section)
            .Select(item => CriterionLine.Collapse(TicketHtmlConverter.ToText(item.Groups["item"].Value)));

    private static List<string> MarkdownItems(string body)
    {
        var lines = body.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var start = Array.FindIndex(lines, line => MarkdownHeading().IsMatch(line));
        var items = new List<string>();
        if (start < 0) return items;
        var inFence = false;
        foreach (var line in lines.Skip(start + 1))
        {
            if (Fence().IsMatch(line)) { inFence = !inFence; continue; }
            if (inFence) continue;
            if (AnyHeading().IsMatch(line)) break;
            Take(items, line);
        }
        return items;
    }

    // An item starts at a marker; an indented line under it — a wrapped line or a nested item —
    // belongs to it, so a sub-point never becomes a criterion of its own.
    private static void Take(List<string> items, string line)
    {
        var isIndented = line.Length > 0 && char.IsWhiteSpace(line[0]) && line.Trim().Length > 0;
        if (isIndented && items.Count > 0)
            items[^1] = $"{items[^1]} {CriterionLine.StripMarker(line)}";
        else if (CriterionLine.ItemText(line) is { } item)
            items.Add(item);
    }

    [GeneratedRegex(@"<h[1-6][^>]*>\s*Acceptance criteria\s*</h[1-6]>(?<section>.*?)(?=<h[1-6][\s>]|\z)",
        RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex HtmlSection();

    [GeneratedRegex(@"<li[^>]*>(?<item>.*?)</li>", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex ListItem();

    [GeneratedRegex(@"^\#{1,6}[ \t]+Acceptance criteria[ \t]*$", RegexOptions.IgnoreCase)]
    private static partial Regex MarkdownHeading();

    [GeneratedRegex(@"^\#{1,6}[ \t]")]
    private static partial Regex AnyHeading();

    [GeneratedRegex(@"^\s*(```|~~~)")]
    private static partial Regex Fence();
}

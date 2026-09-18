using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-09-17-042eb: one line of an acceptance-criteria list, read the same way whether it came
/// from a tracker field or from a body section. Exactly ONE list marker comes off — a bullet, a
/// number or a checkbox — so "- --dry-run exits 0" keeps its flag and "- **the table** exists"
/// keeps its emphasis.
/// </summary>
public static partial class CriterionLine
{
    /// <summary>The text after the line's list marker, or null when the line is not a list item.</summary>
    public static string? ItemText(string line)
    {
        var match = Marker().Match(line);
        return match.Success ? Collapse(line[match.Length..]) : null;
    }

    /// <summary>The line without one leading list marker; a line with none is only trimmed.</summary>
    public static string StripMarker(string line) => ItemText(line) ?? Collapse(line);

    /// <summary>Any run of whitespace, line breaks included, as one space.</summary>
    public static string Collapse(string text) => Whitespace().Replace(text, " ").Trim();

    /// <summary>A placeholder a form or a template leaves where a person wrote nothing.</summary>
    public static bool IsPlaceholder(string text) =>
        text.Length == 0 || text.Equals("_No response_", StringComparison.OrdinalIgnoreCase)
        || (text.StartsWith("<!--", StringComparison.Ordinal) && text.EndsWith("-->", StringComparison.Ordinal));

    [GeneratedRegex(@"^\s*(?:[-*+]|\d+[.)])\s+(?:\[[ xX]\]\s+)?")]
    private static partial Regex Marker();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}

using System.Text.RegularExpressions;

namespace AgentSmith.Domain.Models;

/// <summary>
/// Whether a tracker text carries HTML markup: one of the block or inline tags a tracker's rich-text
/// editor writes. A body without any of them is not HTML in any reading, whatever its field declares —
/// the agent's ticket conversion and the page's rendering both ask this one rule.
/// </summary>
public static partial class HtmlMarkup
{
    public static bool IsPresent(string? body) =>
        !string.IsNullOrEmpty(body) && Tag().IsMatch(body);

    [GeneratedRegex(
        @"</?(p|div|br|li|ul|ol|h[1-6]|pre|code|span|table|thead|tbody|tr|td|th|b|i|u"
        + @"|strong|em|a|img|blockquote|hr)(\s[^>]*)?/?>",
        RegexOptions.IgnoreCase)]
    private static partial Regex Tag();
}

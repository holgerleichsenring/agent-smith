namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-11-12-cd4cl: one page of a file a reviewer reads — from a start line on, cut at a size, and the cut naming the
/// start_line to read on from, so a long file is read through rather than lost past the bound.
/// </summary>
public static class ReadPage
{
    /// <summary>The content from line <paramref name="first"/> on; empty past the end.</summary>
    public static string From(string content, int first)
    {
        ArgumentNullException.ThrowIfNull(content);
        var index = 0;
        for (var line = 1; line < first; line++)
        {
            var next = content.IndexOf('\n', index);
            if (next < 0) return string.Empty;
            index = next + 1;
        }
        return content[index..];
    }

    /// <summary>The page cut at <paramref name="maxChars"/>; the cut names the first line the page did not carry
    /// whole.</summary>
    public static string Bound(string shown, int first, int maxChars)
    {
        ArgumentNullException.ThrowIfNull(shown);
        if (shown.Length <= maxChars) return shown;
        var whole = Math.Max(0, shown[..maxChars].Split('\n').Length - 1);
        return shown[..maxChars] + $"\n… [cut: {shown.Length - maxChars} more characters — read on with "
            + $"start_line={first + whole}]";
    }
}

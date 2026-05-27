namespace AgentSmith.Infrastructure.Core.Services;

/// <summary>
/// Parses markdown sections (## headers) into values and lists.
/// Used by SKILL.md and agentsmith.md parsers.
/// </summary>
internal static class MarkdownSectionParser
{
    internal static string ParseSingleField(string content, string sectionName)
    {
        var header = $"## {sectionName}";
        var headerIndex = content.IndexOf(header, StringComparison.OrdinalIgnoreCase);
        if (headerIndex < 0) return string.Empty;

        var afterHeader = content[(headerIndex + header.Length)..];
        foreach (var line in afterHeader.Split('\n'))
        {
            var val = line.Trim();
            if (!string.IsNullOrWhiteSpace(val) && !val.StartsWith('#'))
                return val;
        }

        return string.Empty;
    }

    internal static List<string> ParseListSection(string content, string sectionName)
    {
        var items = new List<string>();
        var inSection = false;
        var header = $"## {sectionName}";

        foreach (var line in content.Split('\n'))
        {
            var trimmed = line.Trim();

            if (trimmed.Equals(header, StringComparison.OrdinalIgnoreCase))
            {
                inSection = true;
                continue;
            }

            if (inSection && trimmed.StartsWith("## "))
                break;

            if (inSection && trimmed.StartsWith("- "))
            {
                var value = trimmed[2..].Trim().Trim('"');
                if (!string.IsNullOrEmpty(value))
                    items.Add(value);
            }
        }

        return items;
    }
}

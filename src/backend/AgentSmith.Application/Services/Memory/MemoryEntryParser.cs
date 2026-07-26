namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: lenient frontmatter parser for a memory entry file. Expected shape:
/// <c>---</c> / <c>name:</c> / <c>description:</c> / <c>metadata:</c> +
/// indented <c>type:</c> (a top-level <c>type:</c> is accepted too) /
/// optional <c>status:</c> / <c>---</c> / Markdown body. Returns null for any
/// malformed file — the caller skips it with a WARN, never a failure.
/// </summary>
public static class MemoryEntryParser
{
    private const string Fence = "---";

    public static MemoryEntry? TryParse(string fileName, string content)
    {
        var (frontmatter, body) = SplitFrontmatter(content);
        if (frontmatter is null) return null;

        var fields = ParseFields(frontmatter);
        if (!fields.TryGetValue("type", out var typeValue)
            || !MemoryEntryTypes.TryParse(typeValue, out var type))
            return null;

        var name = fields.TryGetValue("name", out var n) && !string.IsNullOrWhiteSpace(n)
            ? n.Trim()
            : Path.GetFileNameWithoutExtension(fileName);
        var description = fields.TryGetValue("description", out var d) ? d.Trim() : string.Empty;
        var status = fields.TryGetValue("status", out var s) && !string.IsNullOrWhiteSpace(s)
            ? s.Trim()
            : null;
        return new MemoryEntry(name, description, type, body.Trim(), status);
    }

    private static (string? Frontmatter, string Body) SplitFrontmatter(string content)
    {
        var trimmed = content.TrimStart('﻿', ' ', '\r', '\n');
        if (!trimmed.StartsWith(Fence, StringComparison.Ordinal)) return (null, content);
        var afterOpen = trimmed.IndexOf('\n');
        if (afterOpen < 0) return (null, content);
        var close = trimmed.IndexOf($"\n{Fence}", afterOpen, StringComparison.Ordinal);
        if (close < 0) return (null, content);
        var frontmatter = trimmed[(afterOpen + 1)..close];
        var bodyStart = trimmed.IndexOf('\n', close + 1);
        return (frontmatter, bodyStart < 0 ? string.Empty : trimmed[(bodyStart + 1)..]);
    }

    private static Dictionary<string, string> ParseFields(string frontmatter)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in frontmatter.Split('\n'))
        {
            var line = raw.TrimEnd('\r');
            var colon = line.IndexOf(':');
            if (colon <= 0) continue;
            var key = line[..colon].Trim();
            var value = line[(colon + 1)..].Trim().Trim('"', '\'');
            if (key.Length == 0 || value.Length == 0) continue;
            fields.TryAdd(key, value); // metadata.type arrives as indented "type:"
        }
        return fields;
    }
}

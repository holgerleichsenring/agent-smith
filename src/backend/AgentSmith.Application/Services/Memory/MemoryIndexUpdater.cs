namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: maintains memory/MEMORY.md — ONE line per memory, mirroring Claude's
/// own MEMORY.md. Content lives in the entry files, never in the index. Upsert
/// replaces the existing line for the same entry name (update-not-duplicate)
/// or appends a new one.
/// </summary>
public static class MemoryIndexUpdater
{
    private const string Header = "# Memory index";

    public static string FormatLine(MemoryEntry entry) =>
        $"- [{entry.Name}]({entry.Name}.md) ({MemoryEntryTypes.ToSlug(entry.Type)}"
        + (entry.Status is null ? string.Empty : $", {entry.Status}")
        + $") — {entry.Description}";

    public static string Upsert(string? existingIndex, MemoryEntry entry)
    {
        var lines = string.IsNullOrWhiteSpace(existingIndex)
            ? new List<string> { Header, string.Empty }
            : existingIndex.Replace("\r\n", "\n").TrimEnd('\n').Split('\n').ToList();

        var marker = $"- [{entry.Name}]({entry.Name}.md)";
        var replaced = false;
        for (var i = 0; i < lines.Count; i++)
        {
            if (!lines[i].StartsWith(marker, StringComparison.Ordinal)) continue;
            lines[i] = FormatLine(entry);
            replaced = true;
            break;
        }
        if (!replaced) lines.Add(FormatLine(entry));
        return string.Join('\n', lines) + "\n";
    }
}

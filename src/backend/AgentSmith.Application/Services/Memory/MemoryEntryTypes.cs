namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: pure slug mapping between <see cref="MemoryEntryType"/> and the
/// lowercase frontmatter value ("feedback" | "project" | "reference").
/// </summary>
public static class MemoryEntryTypes
{
    public static bool TryParse(string? value, out MemoryEntryType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(value)) return false;
        return Enum.TryParse(value.Trim(), ignoreCase: true, out type);
    }

    public static string ToSlug(MemoryEntryType type) => type switch
    {
        MemoryEntryType.Feedback => "feedback",
        MemoryEntryType.Project => "project",
        _ => "reference"
    };
}

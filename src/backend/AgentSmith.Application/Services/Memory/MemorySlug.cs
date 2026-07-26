using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// p0380: pure kebab-slug mapping for memory entry names (= file names).
/// </summary>
public static partial class MemorySlug
{
    public static string ToKebab(string name)
    {
        var lowered = name.Trim().ToLowerInvariant();
        var slug = NonSlugChars().Replace(lowered, "-").Trim('-');
        return DuplicateDashes().Replace(slug, "-");
    }

    [GeneratedRegex("[^a-z0-9-]+")]
    private static partial Regex NonSlugChars();

    [GeneratedRegex("-{2,}")]
    private static partial Regex DuplicateDashes();
}

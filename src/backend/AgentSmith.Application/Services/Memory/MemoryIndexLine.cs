using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Memory;

/// <summary>
/// 2026-10-25-9324d: one entry line of MEMORY.md — <c>- [name](name.md) (type[, label]) — description</c> — read
/// apart so the label can be rewritten without touching what a person or a master wrote around it.
/// </summary>
public sealed partial record MemoryIndexLine(string Name, string File, string? Type, string? Label, string Rest)
{
    public static MemoryIndexLine? TryParse(string line)
    {
        var match = Entry().Match(line);
        if (!match.Success) return null;
        var parens = match.Groups["parens"].Success ? match.Groups["parens"].Value.Split(',', 2) : null;
        return new MemoryIndexLine(match.Groups["name"].Value, match.Groups["file"].Value,
            parens?[0].Trim(), parens is { Length: 2 } ? parens[1].Trim() : null, match.Groups["rest"].Value);
    }

    /// <summary>The line with <paramref name="label"/> beside the type — no label, the type alone.</summary>
    public string Format(string? label) =>
        $"- [{Name}]({File})"
        + (Type is null && label is null ? string.Empty
            : $" ({string.Join(", ", new[] { Type, label }.Where(p => !string.IsNullOrEmpty(p)))})")
        + Rest;

    [GeneratedRegex(@"^- \[(?<name>[^\]]+)\]\((?<file>[^)]+)\)(?: \((?<parens>[^)]*)\))?(?<rest>.*)$")]
    private static partial Regex Entry();
}

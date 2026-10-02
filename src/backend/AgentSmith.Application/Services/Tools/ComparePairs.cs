using AgentSmith.Application.Services.Browser;

namespace AgentSmith.Application.Services.Tools;

/// <summary>
/// 2026-10-01-283di: reads compare_reference's operands — the selector pairs ('a => b', or one
/// selector for both sides, at most <see cref="MaxPairs"/>) and the viewport choice.
/// </summary>
public static class ComparePairs
{
    public const int MaxPairs = 30;
    private const string Arrow = "=>";

    /// <summary>The pairs, or why they cannot be compared.</summary>
    public static (IReadOnlyList<SelectorPair>? Pairs, string? Refusal) Parse(IReadOnlyList<string>? pairs)
    {
        if (pairs is null || pairs.Count == 0) return (null, "name at least one selector pair.");
        if (pairs.Count > MaxPairs) return (null, $"at most {MaxPairs} selector pairs per comparison; {pairs.Count} were given.");
        var mapped = new List<SelectorPair>();
        foreach (var text in pairs)
        {
            var arrow = (text ?? string.Empty).IndexOf(Arrow, StringComparison.Ordinal);
            var (left, right) = arrow < 0 ? (text!.Trim(), text.Trim()) : (text![..arrow].Trim(), text[(arrow + Arrow.Length)..].Trim());
            if (left.Length is 0 or > BrowserStyleProperties.MaxSelectorLength || right.Length is 0 or > BrowserStyleProperties.MaxSelectorLength)
                return (null, $"'{text}' is not a pair of selectors of 1 to {BrowserStyleProperties.MaxSelectorLength} characters.");
            mapped.Add(new SelectorPair(left, right));
        }
        return (mapped, null);
    }

    /// <summary>The viewports to load both sides at, or null for a choice that is none of them.</summary>
    public static IReadOnlyList<string>? Viewports(string? choice) => (choice ?? "desktop").Trim().ToLowerInvariant() switch
    {
        "desktop" or "" => ["desktop"],
        "mobile" => ["mobile"],
        "both" => ["desktop", "mobile"],
        _ => null,
    };
}

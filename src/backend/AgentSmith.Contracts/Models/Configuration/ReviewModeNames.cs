namespace AgentSmith.Contracts.Models.Configuration;

/// <summary>2026-11-25-9992i: the configuration spelling of <see cref="ReviewMode"/> — <c>review:</c>.</summary>
public static class ReviewModeNames
{
    public const string End = "end";
    public const string Each = "each";
    public const string None = "none";

    /// <summary>Every name, in enum order.</summary>
    public static IReadOnlyList<string> All { get; } = [End, Each, None];

    /// <summary>The mode a written value names; null or blank is the default (end), an unknown value is null.</summary>
    public static ReviewMode? Parse(string? written) => written?.Trim().ToLowerInvariant() switch
    {
        null or "" or End => ReviewMode.End,
        Each => ReviewMode.Each,
        None => ReviewMode.None,
        _ => null,
    };

    /// <summary>The YAML name of <paramref name="mode"/>.</summary>
    public static string Of(ReviewMode mode) => mode switch
    {
        ReviewMode.Each => Each,
        ReviewMode.None => None,
        _ => End,
    };

    /// <summary>The value as written, the default's name when nothing is.</summary>
    public static string Written(string? written) => string.IsNullOrWhiteSpace(written) ? End : written.Trim();
}

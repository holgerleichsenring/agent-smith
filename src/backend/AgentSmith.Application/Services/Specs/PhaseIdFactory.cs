using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: the file-name slug of a phase. 2026-10-06-03c7c: its ids are no longer derived from
/// the ticket — a series' base is minted by <see cref="SeriesIdFactory"/>.
/// </summary>
public static partial class PhaseIdFactory
{
    /// <summary>
    /// p0521: how long a generated slug may be — the one number the phase-name rule and
    /// this generator both state. A repo rule tighter than the generator would ship a
    /// product that breaks its own rule in every customer repository.
    /// </summary>
    public const int MaxSlugLength = 50;

    public static string Slug(string goal)
    {
        var slug = NonSlugRegex().Replace(goal.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length == 0) return "phase";
        return slug.Length <= MaxSlugLength ? slug : slug[..MaxSlugLength].TrimEnd('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugRegex();
}

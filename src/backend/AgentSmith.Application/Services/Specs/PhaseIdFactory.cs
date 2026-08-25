using System.Globalization;
using System.Text.RegularExpressions;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// p0393a: phase ids are assigned in CODE, from the ticket, never by the model —
/// an id is an identity a later run must be able to recompute, and a re-cut keeps
/// the executed head's ids by recomputing exactly the same values.
/// <para>
/// Shape is the phase-spec schema's own: <c>p&lt;digits&gt;&lt;letter&gt;</c>, e.g. ticket
/// 19106 becomes p19106a, p19106b, … A ticket id without digits gets a stable
/// four-digit hash so the id is still reproducible.
/// </para>
/// </summary>
public static partial class PhaseIdFactory
{
    /// <summary>
    /// p0521: how long a generated slug may be — the one number the phase-name rule and
    /// this generator both state. A repo rule tighter than the generator would ship a
    /// product that breaks its own rule in every customer repository.
    /// </summary>
    public const int MaxSlugLength = 50;

    [GeneratedRegex("[0-9]+")]
    private static partial Regex DigitsRegex();

    public static string For(string ticketId, int index) =>
        $"p{Digits(ticketId)}{(char)('a' + index)}";

    public static string Slug(string goal)
    {
        var slug = NonSlugRegex().Replace(goal.ToLowerInvariant(), "-").Trim('-');
        if (slug.Length == 0) return "phase";
        return slug.Length <= MaxSlugLength ? slug : slug[..MaxSlugLength].TrimEnd('-');
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex NonSlugRegex();

    private static string Digits(string ticketId)
    {
        var digits = string.Concat(DigitsRegex().Matches(ticketId ?? string.Empty)
            .Select(m => m.Value));
        if (digits.Length == 0) return StableHash(ticketId ?? string.Empty);
        if (digits.Length > 6) digits = digits[^6..];
        return digits.PadLeft(4, '0');
    }

    // Deterministic across processes — string.GetHashCode is randomized per run and
    // an id that changes between runs would break the re-cut's identity rule.
    private static string StableHash(string value)
    {
        var hash = 17u;
        foreach (var c in value) hash = (hash * 31) + c;
        return (hash % 10000).ToString("D4", CultureInfo.InvariantCulture);
    }
}

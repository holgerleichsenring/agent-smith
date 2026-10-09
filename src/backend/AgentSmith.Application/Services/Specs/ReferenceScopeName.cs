using System.Text;

namespace AgentSmith.Application.Services.Specs;

/// <summary>
/// 2026-10-01-283dc: how an uploaded website is addressed in a design turn's sandbox map —
/// <c>reference:&lt;name&gt;</c>, beside the repositories and the <c>template:</c> addresses.
/// The name is the set's, sanitised to lower-case letters, digits and dashes, at most forty
/// characters, <c>upload</c> when nothing survives, and <c>-2</c>, <c>-3</c> for a repeat — so an
/// upload's folder name can never reach the path router as anything but one plain segment.
/// </summary>
public static class ReferenceScopeName
{
    /// <summary>How a reference entry is addressed, so a model and a filter can tell one apart.</summary>
    public const string Prefix = "reference:";

    private const int MaxNameChars = 40;
    // 2026-10-09-86e1: what a name of no letter or digit is addressed as; no longer "site".
    private const string Fallback = "upload";

    /// <summary>The address of each set name, in order, unique among them.</summary>
    public static IReadOnlyList<string> For(IEnumerable<string> names)
    {
        ArgumentNullException.ThrowIfNull(names);
        var taken = new Dictionary<string, int>(StringComparer.Ordinal);
        var addresses = new List<string>();
        foreach (var name in names)
        {
            var clean = Sanitised(name);
            var seen = taken[clean] = taken.GetValueOrDefault(clean) + 1;
            addresses.Add(Prefix + (seen == 1 ? clean : $"{clean}-{seen}"));
        }
        return addresses;
    }

    /// <summary>A set name as an address segment: [a-z0-9-], no leading, trailing or doubled dash.</summary>
    public static string Sanitised(string? name)
    {
        var clean = new StringBuilder();
        foreach (var c in (name ?? string.Empty).ToLowerInvariant())
        {
            var kept = c is >= 'a' and <= 'z' or >= '0' and <= '9' ? c : '-';
            if (kept == '-' && (clean.Length == 0 || clean[^1] == '-')) continue;
            clean.Append(kept);
        }
        var bounded = clean.ToString(0, Math.Min(clean.Length, MaxNameChars)).Trim('-');
        return bounded.Length == 0 ? Fallback : bounded;
    }
}

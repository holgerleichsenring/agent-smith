using AgentSmith.Skills;

namespace AgentSmith.SkillsPackaging;

/// <summary>
/// p0518: the catalog repository declares the description cap its own release gate
/// enforces, in a file it ships inside the tarball. This gate reads that declaration
/// and fails the build when it disagrees with <see cref="SkillDescriptionRule.MaxChars"/>.
/// <para>
/// The two repositories cannot reference each other's source, so a cap raised on
/// either side used to leave the other behind in silence. Here a disagreement stops
/// the build that vendors the catalog — the moment both numbers are visible at once.
/// 2026-08-28-a08d: a catalog carrying no declaration is REFUSED. It used to be
/// accepted because releases before p0518 shipped none — from the embedded pin the
/// file always exists, so its absence is a package missing a file its own release
/// gate writes, not an older release. The gate runs over the vendored tarball
/// alone, so a mounted directory and a customer overlay are out of its reach.
/// </para>
/// </summary>
internal static class CatalogDeclaredCap
{
    public const string EntryPath = "skills/description-cap.txt";

    /// <summary>The cap this build enforces — the number a catalog must declare.</summary>
    public static int Enforced => SkillDescriptionRule.MaxChars;

    public static string? Violation(string? content)
    {
        if (content is null)
            return $"{EntryPath} is missing: the catalog's own release gate writes it, so a "
                + "tarball without it is an incomplete package — and the two caps are then "
                + "invisible to each other again.";
        var declared = Parse(content);
        if (declared is null)
            return $"{EntryPath} declares no cap: expected a line holding just the number.";
        return declared == SkillDescriptionRule.MaxChars
            ? null
            : $"{EntryPath} declares a cap of {declared}, but this build enforces "
              + $"{SkillDescriptionRule.MaxChars}. Both gates carry one number — a description the "
              + "catalog accepts must be one the loader accepts. Move whichever side is wrong.";
    }

    public static bool Matches(string entryName) =>
        entryName == EntryPath || entryName.EndsWith("/" + EntryPath, StringComparison.Ordinal);

    private static int? Parse(string content)
    {
        var declaration = content
            .Split('\n')
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0 && !line.StartsWith('#'));
        return int.TryParse(declaration, out var cap) ? cap : null;
    }
}

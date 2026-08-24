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
/// A catalog released before this file existed declares nothing and is accepted;
/// the per-master cap still gates every description in it.
/// </para>
/// </summary>
internal static class CatalogDeclaredCap
{
    public const string EntryPath = "skills/description-cap.txt";

    public static string? Violation(string? content)
    {
        if (content is null) return null;
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

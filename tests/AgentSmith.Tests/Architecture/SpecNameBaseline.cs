namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521: the specs that were already over a bound when it was installed, in p0403's
/// ratchet shape — a listed spec may only get SHORTER, must LEAVE the list once it
/// fits, and nothing new may join.
/// <para>
/// A ratchet rather than an exemption because the debt is not repayable in place: a
/// finished spec is not edited, and a done spec's file name is a pointer the record
/// already carries. The list keeps the debt visible and lets it move one direction.
/// </para>
/// </summary>
internal static class SpecNameBaseline
{
    private const string BaselineFile = "spec-name-baseline.tsv";

    /// <summary>What each rule measures, and the number it measures against.</summary>
    public const string SlugLength = "slug";

    public const string GoalLength = "goal";

    /// <summary>The embedded schema's own goal limit, which the whole-file check enforces.</summary>
    public const string SchemaGoalLength = "schema-goal";

    /// <summary>
    /// 2026-10-02-3f06b: a date-minted planned spec states no facts. Its value is always 0, and
    /// the row is a debt in the other direction — it leaves once the spec states facts, or
    /// once the spec is no longer planned or active.
    /// </summary>
    public const string FactsRequired = "facts";

    public static IReadOnlyDictionary<(string Rule, string SpecId), int> Rows { get; } = Read();

    public static bool Exempts(string rule, string specId) =>
        Rows.ContainsKey((rule, specId));

    private static IReadOnlyDictionary<(string, string), int> Read() =>
        File.ReadAllLines(Path.Combine(
                ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => (parts[0], parts[2]), parts => int.Parse(parts[1]));
}

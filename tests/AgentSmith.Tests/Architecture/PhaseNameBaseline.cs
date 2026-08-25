namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0521: the phases that were already over a bound when it was installed, in p0403's
/// ratchet shape — a listed phase may only get SHORTER, must LEAVE the list once it
/// fits, and nothing new may join.
/// <para>
/// A ratchet rather than an exemption because the debt is not repayable in place: a
/// finished spec is not edited, and a done phase's file name is a pointer the record
/// already carries. The list keeps the debt visible and lets it move one direction.
/// </para>
/// </summary>
internal static class PhaseNameBaseline
{
    private const string BaselineFile = "phase-name-baseline.tsv";

    /// <summary>What each rule measures, and the number it measures against.</summary>
    public const string SlugLength = "slug";

    public const string GoalLength = "goal";

    /// <summary>The embedded schema's own goal limit, which the whole-file check enforces.</summary>
    public const string SchemaGoalLength = "schema-goal";

    public static IReadOnlyDictionary<(string Rule, string PhaseId), int> Rows { get; } = Read();

    public static bool Exempts(string rule, string phaseId) =>
        Rows.ContainsKey((rule, phaseId));

    private static IReadOnlyDictionary<(string, string), int> Read() =>
        File.ReadAllLines(Path.Combine(
                ArchitectureSources.TestSourceRoot, "Architecture", BaselineFile))
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .Select(line => line.Split('\t'))
            .ToDictionary(parts => (parts[0], parts[2]), parts => int.Parse(parts[1]));
}

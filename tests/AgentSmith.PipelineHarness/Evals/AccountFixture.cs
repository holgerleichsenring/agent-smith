using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgentSmith.PipelineHarness.Evals;

/// <summary>
/// 2026-08-25-7035: one delivery whose truth is known — a base tree, a branch tree, the
/// ratified criteria and the disposition each one deserves.
/// <para>
/// It is a DELIVERY and not a transcript on purpose. Replaying a recorded account cannot
/// measure the account: since p0483 it is a tool-using call that searches live sandboxes,
/// and a replay with no sandbox would score a component that is not the one in production.
/// Two trees can be made into real repositories, so the search tool answers real patterns —
/// including one a future prompt invents, which no recording could.
/// </para>
/// <para>
/// The trees are inline rather than checked-in directories: a fixture is then one reviewable
/// file, and a nested git repository inside this repository is a thing nobody wants to
/// explain twice.
/// </para>
/// </summary>
public sealed record AccountFixture
{
    [JsonPropertyName("id")] public string Id { get; init; } = string.Empty;

    [JsonPropertyName("description")] public string Description { get; init; } = string.Empty;

    /// <summary>What this fixture exists to measure — one of <see cref="CriterionClasses"/>.
    /// The corpus asserts it covers every class it claims, so a class cannot quietly lose its
    /// only fixture.</summary>
    [JsonPropertyName("class")] public string Class { get; init; } = string.Empty;

    /// <summary>Characters per accounting window. A fixture that exists to reproduce a
    /// SPLIT declares a budget small enough to cause one; the rest leave it null and get
    /// production's own number.</summary>
    [JsonPropertyName("window_budget_chars")] public int? WindowBudgetChars { get; init; }

    [JsonPropertyName("repositories")]
    public IReadOnlyList<AccountFixtureRepo> Repositories { get; init; } = [];

    [JsonPropertyName("criteria")]
    public IReadOnlyList<AccountFixtureCriterion> Criteria { get; init; } = [];

    /// <summary>Commands the account is told really ran, as a live run would list them.</summary>
    [JsonPropertyName("commands")] public IReadOnlyList<string> Commands { get; init; } = [];

    public static class CriterionClasses
    {
        public const string Met = "met";
        public const string Unmet = "unmet";
        public const string Absence = "absence";
        public const string VacuousConditional = "vacuous-conditional";
        public const string UniversalAcrossWindows = "universal-across-windows";

        public static IReadOnlyList<string> All =>
            [Met, Unmet, Absence, VacuousConditional, UniversalAcrossWindows];
    }
}

/// <summary>One repository of a fixture delivery: what the base carries and what the branch
/// carries. A path in <see cref="Base"/> and absent from <see cref="Branch"/> is a deletion,
/// which is a real thing a delivery does and a real thing a criterion asks about.</summary>
public sealed record AccountFixtureRepo
{
    [JsonPropertyName("name")] public string Name { get; init; } = string.Empty;

    [JsonPropertyName("base")]
    public IReadOnlyDictionary<string, string> Base { get; init; }
        = new Dictionary<string, string>();

    [JsonPropertyName("branch")]
    public IReadOnlyDictionary<string, string> Branch { get; init; }
        = new Dictionary<string, string>();
}

/// <summary>
/// A ratified criterion and the disposition the branch actually deserves.
/// <para>
/// "Met" here is a property of the TREE, not an opinion about it: whether a file contains an
/// explicit publish route is settled by reading it. A criterion whose truth is a judgement
/// call does not belong in this corpus, because then the fixture author would be the oracle
/// and the eval would measure the author.
/// </para>
/// </summary>
public sealed record AccountFixtureCriterion
{
    [JsonPropertyName("text")] public string Text { get; init; } = string.Empty;

    /// <summary>"met" or "unmet" — what a correct account answers.</summary>
    [JsonPropertyName("truth")] public string Truth { get; init; } = string.Empty;

    /// <summary>Why it is true, for the human reading a failing report.</summary>
    [JsonPropertyName("because")] public string Because { get; init; } = string.Empty;

    public const string TruthMet = "met";
    public const string TruthUnmet = "unmet";

    public bool IsMet => string.Equals(Truth, TruthMet, StringComparison.Ordinal);

    public bool HasKnownTruth =>
        Truth is TruthMet or TruthUnmet;
}

/// <summary>
/// 2026-08-25-7035: loads the corpus and refuses a fixture carrying a customer fingerprint.
/// The material here is authored rather than harvested, so there is nothing to anonymise —
/// which is exactly why the gate runs: the day someone pastes a real tree into a fixture is
/// the day it stops being true.
/// </summary>
public static class AccountFixtureLoader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
    };

    public static IReadOnlyList<AccountFixture> LoadAll(string directory)
    {
        if (!Directory.Exists(directory)) return [];
        return
        [
            .. Directory.EnumerateFiles(directory, "*.json")
                .OrderBy(path => path, StringComparer.Ordinal)
                .Select(path => Load(path, directory)),
        ];
    }

    public static AccountFixture Load(string path, string? denyListDirectory)
    {
        var raw = File.ReadAllText(path);
        var violations = ExpectationFixtureAnonymizationCheck.CheckText(raw, denyListDirectory);
        if (violations.Count > 0)
            throw new InvalidOperationException(
                $"{Path.GetFileName(path)} carries customer fingerprints and will not load:\n  "
                + string.Join("\n  ", violations));
        return JsonSerializer.Deserialize<AccountFixture>(raw, Options)
            ?? throw new InvalidOperationException($"{Path.GetFileName(path)} is not a fixture.");
    }
}

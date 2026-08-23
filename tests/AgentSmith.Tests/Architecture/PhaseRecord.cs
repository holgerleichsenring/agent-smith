using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0430/p0509: the phase record this repository states — the specs under
/// <c>.agentsmith/phases</c>, the context that says what shipped, and the three ways the
/// two can disagree. How an id is read is a parameter, so a change to the reading can be
/// held against the reading it replaced.
/// </summary>
internal sealed class PhaseRecord(PhaseIdReader reader)
{
    private static readonly Regex ContextSectionPattern =
        new(@"^  (?<section>done|active|planned):", RegexOptions.Multiline | RegexOptions.Compiled);

    public static PhaseRecord Current { get; } = new(PhaseIdReader.Current);

    /// <summary>A phase id that resolves to more than one spec file.</summary>
    public IReadOnlyCollection<string> DuplicateIds() =>
    [
        .. Specs().GroupBy(s => s.PhaseId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
    ];

    /// <summary>A spec in done/ that state.done in the context never recorded.</summary>
    public IReadOnlyCollection<string> UnrecordedDonePhases()
    {
        var recorded = ContextPhaseIds();
        return [.. Specs()
            .Where(s => s.Stage == "done" && !recorded.Contains(s.PhaseId))
            .Select(s => s.PhaseId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];
    }

    /// <summary>
    /// A <c>requires:</c> naming a phase no file and no context entry defines. Scoped to
    /// ids the record KNOWS: a phase that shipped before the record existed is
    /// legitimately named and has no file. What must not happen is a reference to a phase
    /// this repository never wrote down at all.
    /// </summary>
    public IReadOnlyCollection<string> DanglingRequires()
    {
        var specs = Specs();
        var known = specs.Select(s => s.PhaseId).ToHashSet(StringComparer.Ordinal);
        var recorded = ContextPhaseIds();
        return [.. specs
            .SelectMany(s => reader.Requires(s.Text).Select(r => (s.PhaseId, Required: r)))
            .Where(x => !known.Contains(x.Required) && !recorded.Contains(x.Required))
            .Select(x => $"{x.PhaseId} -> {x.Required}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];
    }

    public HashSet<string> ContextIdsUnder(string section)
    {
        var yaml = ContextYaml();
        var sections = ContextSectionPattern.Matches(yaml);
        var start = sections.FirstOrDefault(m => m.Groups["section"].Value == section);
        if (start is null) return [];
        var next = sections.FirstOrDefault(m => m.Index > start.Index);
        return Ids(yaml[start.Index..(next?.Index ?? yaml.Length)]);
    }

    public HashSet<string> ContextPhaseIds() => Ids(ContextYaml());

    private HashSet<string> Ids(string yaml) =>
        reader.ContextId.Matches(yaml).Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private sealed record Spec(string PhaseId, string Stage, string Text);

    private IReadOnlyList<Spec> Specs() =>
    [
        .. new[] { "done", "active", "planned" }.SelectMany(stage =>
            SpecFiles(stage)
                .Select(path =>
                {
                    var text = File.ReadAllText(path);
                    var match = reader.SpecId.Match(text);
                    match.Success.Should().BeTrue($"{path} must declare a `phase:` id");
                    return new Spec(match.Groups["id"].Value, stage, text);
                }))
    ];

    /// <summary>
    /// An ABSENT stage directory is an empty one, never a crash. Git cannot store an
    /// empty directory, so the moment p0430 achieved its own goal — nothing left in
    /// active/ — the directory stopped existing on every fresh clone and the rule threw
    /// before it could judge anything. A gate that fails the healthiest possible state is
    /// p0428's lesson in a new costume.
    /// </summary>
    private static IEnumerable<string> SpecFiles(string stage)
    {
        var dir = Path.Combine(RepoRoot(), ".agentsmith", "phases", stage);
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*.yaml") : [];
    }

    private static string ContextYaml() =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), ".agentsmith", "contexts", "default", "context.yaml"));

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".agentsmith", "phases"))) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }

        throw new InvalidOperationException(
            $"Could not locate the repo root from '{AppContext.BaseDirectory}'");
    }
}

using System.Text.RegularExpressions;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// p0430: the phase record states what the repository actually did, enforced rather
/// than remembered.
/// <para>
/// Steps 8 and 9 of the workflow — update the context, move the phase to done/ — are the
/// ones that get skipped, every time, because they come after the work is already proven.
/// On 2026-08-17 that had left nine shipped phases in active/, eleven phases missing from
/// the context, two phase ids resolving to two files each, and a <c>requires: p0422</c>
/// pointing at a spec nobody ever wrote. A rule nobody can forget beats a rule everyone
/// agrees with.
/// </para>
/// <para>
/// The 400 phases already on the record do not get an exemption — they get p0403's
/// RATCHET. The baseline lists every violation as it stood when the rule was installed;
/// from here nothing new may join it, and an entry that is fixed must leave it. Writing
/// a plausible history for a phase from 2026-03 would be invention, which is worse than
/// a named gap; the ratchet keeps the gap named and the debt one-directional.
/// </para>
/// </summary>
public sealed class PhaseRecordRuleTests
{
    private const string BaselineFile = "phase-record-baseline.tsv";
    private const string WriteFlag = "AGENTSMITH_WRITE_PHASE_BASELINE";

    [Fact]
    public void PhaseRecord_EveryPhaseId_ResolvesToOneFile() =>
        Assert("duplicate-id", DuplicateIds(),
            "a phase id that resolves to two files makes every `requires:` naming it "
            + "ambiguous. Renumber the one that has not shipped.");

    [Fact]
    public void PhaseRecord_EveryDonePhase_IsRecordedInTheContext() =>
        Assert("unrecorded", UnrecordedDonePhases(),
            "a phase in done/ has shipped, and the context is where the next agent reads "
            + "what shipped. Add it to state.done in contexts/default/context.yaml.");

    [Fact]
    public void PhaseRecord_EveryRequires_NamesAPhaseThatExists() =>
        Assert("dangling", DanglingRequires(),
            "a `requires:` naming a phase nobody wrote is a dependency on a document that "
            + "does not exist. Write the spec, or drop the reference.");

    /// <summary>
    /// A phase cannot be both shipped and upcoming. No baseline: the overlap was two
    /// entries, both fixed in this phase, so there is no debt to ratchet.
    /// </summary>
    [Fact]
    public void PhaseRecord_NoShippedPhase_IsStillListedAsPlanned()
    {
        var stale = ContextIdsUnder("planned")
            .Intersect(ContextIdsUnder("done"), StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        stale.Should().BeEmpty(
            "a phase cannot be both shipped and upcoming — the planned entry is stale.\n  "
            + string.Join("\n  ", stale));
    }

    /// <summary>
    /// The ratchet: nothing new may be added, and nothing fixed may stay. Regenerate with
    /// <c>AGENTSMITH_WRITE_PHASE_BASELINE=1</c> only when the baseline SHRINKS.
    /// </summary>
    private static void Assert(string kind, IReadOnlyCollection<string> violations, string because)
    {
        if (Environment.GetEnvironmentVariable(WriteFlag) == "1") { Rewrite(); return; }

        var baseline = Baseline(kind);
        var added = violations.Where(v => !baseline.Contains(v))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        added.Should().BeEmpty(
            $"{because}\n  " + string.Join("\n  ", added));

        var fixedEntries = baseline.Except(violations, StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal).ToList();
        fixedEntries.Should().BeEmpty(
            $"the debt only moves one way — remove the fixed '{kind}' entries from "
            + $"{BaselineFile}.\n  " + string.Join("\n  ", fixedEntries));
    }

    private static void Rewrite()
    {
        var rows = DuplicateIds().Select(v => $"duplicate-id\t{v}")
            .Concat(UnrecordedDonePhases().Select(v => $"unrecorded\t{v}"))
            .Concat(DanglingRequires().Select(v => $"dangling\t{v}"))
            .OrderBy(x => x, StringComparer.Ordinal);
        File.WriteAllText(BaselinePath(), string.Join("\n", rows) + "\n");
    }

    private static IReadOnlyCollection<string> DuplicateIds() =>
    [
        .. Specs().GroupBy(s => s.PhaseId, StringComparer.Ordinal)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .OrderBy(x => x, StringComparer.Ordinal)
    ];

    private static IReadOnlyCollection<string> UnrecordedDonePhases()
    {
        var recorded = ContextPhaseIds();
        return [.. Specs()
            .Where(s => s.Stage == "done" && !recorded.Contains(s.PhaseId))
            .Select(s => s.PhaseId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];
    }

    private static IReadOnlyCollection<string> DanglingRequires()
    {
        // Scoped to ids the record KNOWS: a phase that shipped before the record existed
        // is legitimately named by a `requires:` and has no file. What must not happen is
        // a reference to a phase this repository never wrote down at all.
        var specs = Specs();
        var known = specs.Select(s => s.PhaseId).ToHashSet(StringComparer.Ordinal);
        var recorded = ContextPhaseIds();
        return [.. specs
            .SelectMany(s => Requires(s.Text).Select(r => (s.PhaseId, Required: r)))
            .Where(x => !known.Contains(x.Required) && !recorded.Contains(x.Required))
            .Select(x => $"{x.PhaseId} -> {x.Required}")
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)];
    }

    private sealed record Spec(string PhaseId, string Stage, string Text);

    private static readonly Regex PhaseIdPattern =
        new(@"^\s*phase:\s*""?(?<id>p\d{4}[a-z]?)", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ContextIdPattern =
        new(@"^    (?<id>p\d{4}[a-z]?):", RegexOptions.Multiline | RegexOptions.Compiled);

    private static readonly Regex ContextSectionPattern =
        new(@"^  (?<section>done|active|planned):", RegexOptions.Multiline | RegexOptions.Compiled);

    /// <summary>
    /// An ABSENT stage directory is an empty one, never a crash. Git cannot store an
    /// empty directory, so the moment this phase achieved its own goal — nothing left in
    /// active/ — the directory stopped existing on every fresh clone and the rule threw
    /// before it could judge anything. A gate that fails the healthiest possible state is
    /// p0428's lesson in a new costume. The .gitkeep beside it keeps the directory
    /// legible to a reader; this keeps the rule honest without depending on it.
    /// </summary>
    private static IEnumerable<string> SpecFiles(string stage)
    {
        var dir = Path.Combine(PhasesRoot(), stage);
        return Directory.Exists(dir) ? Directory.EnumerateFiles(dir, "*.yaml") : [];
    }

    private static IReadOnlyList<Spec> Specs() =>
    [
        .. new[] { "done", "active", "planned" }.SelectMany(stage =>
            SpecFiles(stage)
                .Select(path =>
                {
                    var text = File.ReadAllText(path);
                    var match = PhaseIdPattern.Match(text);
                    match.Success.Should().BeTrue($"{path} must declare a `phase:` id");
                    return new Spec(match.Groups["id"].Value, stage, text);
                }))
    ];

    private static IEnumerable<string> Requires(string text)
    {
        // Both spellings the specs use: an inline list and a block list.
        var inline = Regex.Match(text, @"^requires:\s*\[(?<items>[^\]]*)\]", RegexOptions.Multiline);
        if (inline.Success)
            return Regex.Matches(inline.Groups["items"].Value, @"p\d{4}[a-z]?").Select(m => m.Value);

        var block = Regex.Match(
            text, @"^requires:\s*\n(?<body>(?:[ \t]*-[ \t]*.*\n)+)", RegexOptions.Multiline);
        return block.Success
            ? Regex.Matches(block.Groups["body"].Value, @"^\s*-\s*(?<id>p\d{4}[a-z]?)", RegexOptions.Multiline)
                .Select(m => m.Groups["id"].Value)
            : [];
    }

    private static HashSet<string> Baseline(string kind) =>
        File.ReadAllLines(BaselinePath())
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[0] == kind)
            .Select(parts => parts[1])
            .ToHashSet(StringComparer.Ordinal);

    private static string BaselinePath() =>
        Path.Combine(SourceDirectory(), BaselineFile);

    private static HashSet<string> ContextPhaseIds() =>
        ContextIdPattern.Matches(ContextYaml())
            .Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ContextIdsUnder(string section)
    {
        var yaml = ContextYaml();
        var sections = ContextSectionPattern.Matches(yaml);
        var start = sections.FirstOrDefault(m => m.Groups["section"].Value == section);
        if (start is null) return [];
        var next = sections.FirstOrDefault(m => m.Index > start.Index);
        var body = yaml[start.Index..(next?.Index ?? yaml.Length)];
        return ContextIdPattern.Matches(body)
            .Select(m => m.Groups["id"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string ContextYaml() =>
        File.ReadAllText(Path.Combine(
            RepoRoot(), ".agentsmith", "contexts", "default", "context.yaml"));

    private static string PhasesRoot() => Path.Combine(RepoRoot(), ".agentsmith", "phases");

    private static string SourceDirectory() =>
        Path.Combine(RepoRoot(), "tests", "AgentSmith.Tests", "Architecture");

    private static string RepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        for (var i = 0; i < 10; i++)
        {
            if (Directory.Exists(Path.Combine(dir, ".agentsmith", "phases"))) return dir;
            dir = Directory.GetParent(dir)?.FullName ?? dir;
        }

        throw new InvalidOperationException($"Could not locate the repo root from '{AppContext.BaseDirectory}'");
    }
}

using AgentSmith.Application.Services.Specs;
using AgentSmith.Tests.Architecture;
using FluentAssertions;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// 2026-10-02-3f06e: the evidence grammar's parse cases, copied verbatim from the spec-first
/// plugin's skills/review-spec/evidence-cases.yaml, which its check-evidence.py runs as a
/// self-test. Two repositories cannot read each other in CI, so the copy is a convention —
/// change the file in both — and this test is what makes a one-sided edit of THIS reader go red.
/// Only parsing is shared: resolution is each side's own policy.
/// </summary>
public sealed class EvidenceSharedCasesTests
{
    private const string CaseFile = "evidence-cases.yaml";

    private readonly EvidenceReferences _reader = new();

    [Fact]
    public void EvidenceReferences_SharedCases_MatchThePluginScript()
    {
        var cases = Cases();
        cases.Should().HaveCountGreaterThan(10, "an unread case file must not pass as agreement");
        cases.Should().OnlyContain(c => c.Evidence.Length > 0);

        var failures = cases
            .Select(c => (Case: c, Reading: _reader.Read(c.Evidence)))
            .Where(r => !Matches(r.Case, r.Reading))
            .Select(r => $"{r.Case.Evidence} → {Describe(r.Reading)}")
            .ToList();

        failures.Should().BeEmpty("the plugin's script reads these cases the same way.\n  "
            + string.Join("\n  ", failures));
    }

    private static bool Matches(SharedCase expected, EvidenceReading reading) =>
        reading.Observations.Count == expected.Observations
        && reading.Minted.Count == expected.Minted
        && reading.References.Select(Key).SequenceEqual(expected.References.Select(Key));

    private static string Key(EvidenceReference r) => $"{r.Qualifier}|{r.Path}|{r.LineText}";

    private static string Key(SharedReference r) => $"{r.Qualifier}|{r.Path}|{r.Lines}";

    private static string Describe(EvidenceReading r) =>
        $"[{string.Join(", ", r.References.Select(Key))}] observed {r.Observations.Count} minted {r.Minted.Count}";

    private static IReadOnlyList<SharedCase> Cases() =>
        new DeserializerBuilder().WithNamingConvention(CamelCaseNamingConvention.Instance).Build()
            .Deserialize<List<SharedCase>>(File.ReadAllText(
                Path.Combine(ArchitectureSources.TestSourceRoot, "Specs", CaseFile)));

    public sealed class SharedCase
    {
        public string Evidence { get; set; } = string.Empty;
        public List<SharedReference> References { get; set; } = [];
        public int Observations { get; set; }
        public int Minted { get; set; }
    }

    public sealed class SharedReference
    {
        public string? Qualifier { get; set; }
        public string Path { get; set; } = string.Empty;
        public string? Lines { get; set; }
    }
}

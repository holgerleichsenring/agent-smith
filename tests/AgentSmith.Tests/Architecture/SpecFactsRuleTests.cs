using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-02-3f06b: a date-minted spec in planned/ states at least one fact. One dated
/// observation satisfies it — accepted: the rule asks that a plan say what it rests on, and the
/// evidence rule asks that what it says resolves.
/// <para>
/// The specs that predate the rule are baselined as <see cref="SpecNameBaseline.FactsRequired"/>
/// rows, and the ratchet runs the other way to the length rules: a row leaves once its spec
/// states facts, or once it is no longer planned or active. The closed counter namespace is not
/// judged — the rule is scoped by namespace, as every spec rule here.
/// </para>
/// </summary>
public sealed class SpecFactsRuleTests
{
    [Fact]
    public void SpecFacts_NewDateMintedSpec_StatesFacts()
    {
        var offenders = SpecYamlFile.All()
            .Where(IsJudged)
            .Where(file => !file.StatesFacts && !SpecNameBaseline.Exempts(SpecNameBaseline.FactsRequired, file.SpecId))
            .Select(file => $"{file.SpecId}-{file.Slug}")
            .ToList();

        offenders.Should().BeEmpty(
            "a planned spec states the facts it rests on, each with evidence that resolves. "
            + "Do not add a baseline row.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void SpecFacts_EmptyList_IsNotStating()
    {
        Stating("facts: []").Should().BeFalse();
        Stating("facts:").Should().BeFalse();
        Stating("facts: [{claim: c, evidence: \"observed: a scan, 2026-10-02\"}]").Should().BeTrue();
    }

    [Fact]
    public void SpecFacts_CounterIdSpec_IsNotJudged()
    {
        var counter = SpecYamlFile.All().Where(file => file.State == "planned" && !file.IsDateMinted).ToList();

        counter.Should().Contain(file => !file.StatesFacts,
            "the closed namespace holds planned specs without facts, and none is ever rewritten");
        counter.Should().NotContain(file => IsJudged(file));
    }

    [Fact]
    public void SpecFacts_StaleBaselineRow_Fails()
    {
        var files = SpecYamlFile.All();

        Stale(SpecNameBaseline.Rows.Keys, files).Should().BeEmpty(
            "a spec that now states facts, or left planned/ and active/, must leave "
            + "spec-name-baseline.tsv");
        Stale([(SpecNameBaseline.FactsRequired, "2026-10-02-3f06f")], files).Should().ContainSingle(
            "a row naming a done spec is stale");
    }

    private static bool IsJudged(SpecYamlFile file) => file.IsDateMinted && file.State == "planned";

    private static IReadOnlyList<string> Stale(
        IEnumerable<(string Rule, string SpecId)> rows, IReadOnlyList<SpecYamlFile> files) =>
    [
        .. rows
            .Where(row => row.Rule == SpecNameBaseline.FactsRequired)
            .Where(row => !files.Any(file => file.SpecId == row.SpecId
                && file.State is "planned" or "active" && !file.StatesFacts))
            .Select(row => row.SpecId),
    ];

    private static bool Stating(string facts) =>
        SpecYamlFile.States(YamlAsJson.Convert($"spec: 2026-10-02-0000\ngoal: g\n{facts}\n"));
}

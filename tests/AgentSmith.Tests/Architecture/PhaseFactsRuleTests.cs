using AgentSmith.Application.Services.Validation;
using FluentAssertions;

namespace AgentSmith.Tests.Architecture;

/// <summary>
/// 2026-10-02-3f06b: a date-minted phase in planned/ states at least one fact. One dated
/// observation satisfies it — accepted: the rule asks that a plan say what it rests on, and the
/// evidence rule asks that what it says resolves.
/// <para>
/// The phases that predate the rule are baselined as <see cref="PhaseNameBaseline.FactsRequired"/>
/// rows, and the ratchet runs the other way to the length rules: a row leaves once its phase
/// states facts, or once it is no longer planned or active. The closed counter namespace is not
/// judged — the rule is scoped by namespace, as every phase rule here.
/// </para>
/// </summary>
public sealed class PhaseFactsRuleTests
{
    [Fact]
    public void PhaseFacts_NewDateMintedPhase_StatesFacts()
    {
        var offenders = PhaseSpecFile.All()
            .Where(IsJudged)
            .Where(file => !file.StatesFacts && !PhaseNameBaseline.Exempts(PhaseNameBaseline.FactsRequired, file.PhaseId))
            .Select(file => $"{file.PhaseId}-{file.Slug}")
            .ToList();

        offenders.Should().BeEmpty(
            "a planned phase states the facts it rests on, each with evidence that resolves. "
            + "Do not add a baseline row.\n  " + string.Join("\n  ", offenders));
    }

    [Fact]
    public void PhaseFacts_EmptyList_IsNotStating()
    {
        Stating("facts: []").Should().BeFalse();
        Stating("facts:").Should().BeFalse();
        Stating("facts: [{claim: c, evidence: \"observed: a scan, 2026-10-02\"}]").Should().BeTrue();
    }

    [Fact]
    public void PhaseFacts_CounterIdPhase_IsNotJudged()
    {
        var counter = PhaseSpecFile.All().Where(file => file.State == "planned" && !file.IsDateMinted).ToList();

        counter.Should().Contain(file => !file.StatesFacts,
            "the closed namespace holds planned phases without facts, and none is ever rewritten");
        counter.Should().NotContain(file => IsJudged(file));
    }

    [Fact]
    public void PhaseFacts_StaleBaselineRow_Fails()
    {
        var files = PhaseSpecFile.All();

        Stale(PhaseNameBaseline.Rows.Keys, files).Should().BeEmpty(
            "a phase that now states facts, or left planned/ and active/, must leave "
            + "phase-name-baseline.tsv");
        Stale([(PhaseNameBaseline.FactsRequired, "2026-10-02-3f06f")], files).Should().ContainSingle(
            "a row naming a done phase is stale");
    }

    private static bool IsJudged(PhaseSpecFile file) => file.IsDateMinted && file.State == "planned";

    private static IReadOnlyList<string> Stale(
        IEnumerable<(string Rule, string PhaseId)> rows, IReadOnlyList<PhaseSpecFile> files) =>
    [
        .. rows
            .Where(row => row.Rule == PhaseNameBaseline.FactsRequired)
            .Where(row => !files.Any(file => file.PhaseId == row.PhaseId
                && file.State is "planned" or "active" && !file.StatesFacts))
            .Select(row => row.PhaseId),
    ];

    private static bool Stating(string facts) =>
        PhaseSpecFile.States(YamlAsJson.Convert($"phase: 2026-10-02-0000\ngoal: g\n{facts}\n"));
}

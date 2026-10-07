using AgentSmith.Application.Services.Expectations;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Services.Expectations;

/// <summary>
/// 2026-09-06-4a2c: the master is given the criteria it will be judged by.
/// <para>
/// p0393 made the derived spec the acceptance contract but left this section reading the
/// retired negotiation key, so the token rendered empty on every spec-derived run. The
/// master's skill makes its `acceptance` array REQUIRED only when an "## Acceptance contract"
/// section was given, so it correctly omitted the array, the gate found no dispositions, and
/// MasterAcceptanceGate could never return true — on a live run that had already finished its
/// work, committed it and passed verification green.
/// </para>
/// </summary>
public sealed class AcceptanceContractSectionTests
{
    private const string First = "An audit of the repository reports zero high-severity findings.";
    private const string Second = "The lint command exits 0.";

    private static PipelineContext WithPhaseSpec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(
            ContextKeys.PhaseSpec,
            new PhaseDraft("p1", "Upgrade the flagged packages", "spec: p1", [])
            {
                Done = [First, Second],
            });
        return pipeline;
    }

    private static IReadOnlyList<string> Bullets(string section) =>
        [.. section.Split('\n')
            .Where(l => l.StartsWith("- ", StringComparison.Ordinal))
            .Select(l => l[2..].Trim())];

    [Fact]
    public void Acceptance_ASpecDerivedRun_PresentsAnAcceptanceContractSection()
    {
        var section = ExpectationPromptSection.Build(WithPhaseSpec());

        // The heading is the condition the pinned master puts on answering at all.
        section.Should().Contain("## Acceptance contract");
    }

    [Fact]
    public void Acceptance_TheContractSection_CarriesEveryRatifiedCriterion()
    {
        var section = ExpectationPromptSection.Build(WithPhaseSpec());

        Bullets(section).Should().Equal(First, Second);
    }

    [Fact]
    public void Acceptance_TheContractSection_NamesTheExpectedAssertionsTheSkillAsksFor()
    {
        var section = ExpectationPromptSection.Build(WithPhaseSpec());

        // The skill says "one entry per ratified 'Expected' assertion" and "walk EACH
        // 'Expected' assertion" — instructions that need something of that name to land on.
        section.Should().Contain("## Expected");
    }

    [Fact]
    public void Acceptance_ThePromptAndTheGate_ResolveTheSameCriteria()
    {
        var pipeline = WithPhaseSpec();

        var presented = Bullets(ExpectationPromptSection.Build(pipeline));

        // The one invariant this phase exists for: asked about exactly what it is judged by.
        presented.Should().Equal(AcceptanceCriteria.For(pipeline));
    }

    /// <summary>
    /// 2026-09-06-3d81: the third answer is stated in the contract. AcceptanceStatus has had
    /// not_applicable since p0340 and the gate has accepted it with a reason all along; the
    /// master was never told, so a criterion the repository makes impossible was fought pass
    /// after pass (run 989e bisected plugin versions for sixteen minutes) or, when a master
    /// did decline it, the answer reached nobody.
    /// </summary>
    [Fact]
    public void Contract_TheSectionGivenToTheMaster_NamesNotApplicableAndItsEvidenceRule()
    {
        var derived = ExpectationPromptSection.Build(WithPhaseSpec());

        derived.Should().Contain("`not_applicable`", "the answer is named in the vocabulary the skill parses");
        derived.Should().Contain("EVALUATED MEANING", "and what it costs to use it — the skill's own words");
        derived.Should().Contain("does not count", "a bare N/A is refused, and the master is told so");
        // The rule is prose; the criteria list the gate reads back is untouched.
        Bullets(derived).Should().Equal(First, Second);
    }

    [Fact]
    public void Acceptance_ARunWithNeitherSpecNorExpectation_RendersNoContractSection()
    {
        ExpectationPromptSection.Build(new PipelineContext()).Should().BeEmpty();
    }
}

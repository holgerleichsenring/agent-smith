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

    private static PipelineContext WithSpec()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(
            ContextKeys.CurrentSpec,
            new SpecDraft("p1", "Upgrade the flagged packages", "spec: p1", [])
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
        var section = ExpectationPromptSection.Build(WithSpec());

        // The heading is the condition the pinned master puts on answering at all.
        section.Should().Contain("## Acceptance contract");
    }

    [Fact]
    public void Acceptance_TheContractSection_CarriesEveryRatifiedCriterion()
    {
        var section = ExpectationPromptSection.Build(WithSpec());

        Bullets(section).Should().Equal(First, Second);
    }

    [Fact]
    public void Acceptance_TheContractSection_NamesTheExpectedAssertionsTheSkillAsksFor()
    {
        var section = ExpectationPromptSection.Build(WithSpec());

        // The skill says "one entry per ratified 'Expected' assertion" and "walk EACH
        // 'Expected' assertion" — instructions that need something of that name to land on.
        section.Should().Contain("## Expected");
    }

    [Fact]
    public void Acceptance_ThePromptAndTheGate_ResolveTheSameCriteria()
    {
        var pipeline = WithSpec();

        var presented = Bullets(ExpectationPromptSection.Build(pipeline));

        // The one invariant this spec exists for: asked about exactly what it is judged by.
        presented.Should().Equal(AcceptanceCriteria.For(pipeline));
    }

    /// <summary>
    /// 2026-11-18-98fa: the done list reaches the master as its spec's list, with no per-line vocabulary to answer in —
    /// its report says what it saw for each, a line left says why, and missing tooling or a red test is work.
    /// </summary>
    [Fact]
    public void Contract_TheSectionGivenToTheMaster_AsksForTheReportNotADisposition()
    {
        var derived = ExpectationPromptSection.Build(WithSpec());

        derived.Should().Contain("Your report says what you saw for each; a line you leave says why");
        derived.Should().Contain("A red test is work.");
        derived.Should().NotContain("not_applicable", "nothing is judged per line any more");
        // The rule is prose; the criteria list the gate reads back is untouched.
        Bullets(derived).Should().Equal(First, Second);
    }

    [Fact]
    public void Acceptance_ARunWithNeitherSpecNorExpectation_RendersNoContractSection()
    {
        ExpectationPromptSection.Build(new PipelineContext()).Should().BeEmpty();
    }
}

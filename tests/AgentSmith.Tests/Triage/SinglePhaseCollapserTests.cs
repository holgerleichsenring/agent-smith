using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Skills;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class SinglePhaseCollapserTests
{
    private readonly SinglePhaseCollapser _sut = new();

    [Fact]
    public void Collapse_OnlyPlanPopulated_OutputUnchanged()
    {
        var input = TriageWith(plan: Phase("lead", ["a1"], [], filter: "f"));

        var result = _sut.Collapse(input);

        result.Phases.Should().ContainKey(PipelinePhase.Plan);
        result.Phases.Should().NotContainKey(PipelinePhase.Review);
        result.Phases.Should().NotContainKey(PipelinePhase.Final);
        result.Phases[PipelinePhase.Plan].Lead.Should().Be("lead");
        result.Phases[PipelinePhase.Plan].Analysts.Should().Equal("a1");
        result.Phases[PipelinePhase.Plan].Filter.Should().Be("f");
    }

    [Fact]
    public void Collapse_ReviewAnalysts_MergedIntoPlanAnalysts()
    {
        var input = TriageWith(
            plan: Phase("lead", ["a1"], [], filter: null),
            review: Phase(null, ["a2", "a3"], ["r1"], filter: null));

        var result = _sut.Collapse(input);

        result.Phases[PipelinePhase.Plan].Analysts.Should().Equal("a1", "a2", "a3", "r1");
        result.Phases[PipelinePhase.Plan].Reviewers.Should().BeEmpty();
    }

    [Fact]
    public void Collapse_FinalPhaseRolesAlsoMergedIntoPlan()
    {
        var input = TriageWith(
            plan: Phase(null, [], [], filter: null),
            review: Phase(null, ["r-a"], [], filter: null),
            final: Phase("ignored-lead", ["f-a"], ["f-r"], filter: null));

        var result = _sut.Collapse(input);

        // Plan-phase Lead stays Lead (or null if Plan had none); Final-phase Lead is dropped.
        result.Phases[PipelinePhase.Plan].Lead.Should().BeNull();
        result.Phases[PipelinePhase.Plan].Analysts.Should().Equal("r-a", "f-a", "f-r");
    }

    [Fact]
    public void Collapse_DuplicateNames_DeduplicatedFirstWins()
    {
        // Plan analyst "a1" should not duplicate when Review/Final mention it again.
        var input = TriageWith(
            plan: Phase("lead", ["a1"], [], filter: null),
            review: Phase(null, ["a1"], ["a2"], filter: null),
            final: Phase(null, ["a1"], ["a2"], filter: null));

        var result = _sut.Collapse(input);

        result.Phases[PipelinePhase.Plan].Analysts.Should().Equal("a1", "a2");
    }

    [Fact]
    public void Collapse_FilterFallsBackToReviewThenFinal()
    {
        var input = TriageWith(
            plan: Phase(null, [], [], filter: null),
            review: Phase(null, [], [], filter: "review-filter"),
            final: Phase(null, [], [], filter: "final-filter"));

        var result = _sut.Collapse(input);

        result.Phases[PipelinePhase.Plan].Filter.Should().Be("review-filter");
    }

    [Fact]
    public void Collapse_PreservesConfidenceAndRationale()
    {
        var input = new TriageOutput(
            Phases: new Dictionary<PipelinePhase, PhaseAssignment> { [PipelinePhase.Plan] = Phase("l", [], [], null) },
            Confidence: 87,
            Rationale: "test rationale");

        var result = _sut.Collapse(input);

        result.Confidence.Should().Be(87);
        result.Rationale.Should().Be("test rationale");
    }

    private static PhaseAssignment Phase(string? lead, IReadOnlyList<string> analysts,
        IReadOnlyList<string> reviewers, string? filter) =>
        new(lead, analysts, reviewers, filter);

    private static TriageOutput TriageWith(
        PhaseAssignment? plan = null, PhaseAssignment? review = null, PhaseAssignment? final = null)
    {
        var phases = new Dictionary<PipelinePhase, PhaseAssignment>();
        if (plan is not null) phases[PipelinePhase.Plan] = plan;
        if (review is not null) phases[PipelinePhase.Review] = review;
        if (final is not null) phases[PipelinePhase.Final] = final;
        return new TriageOutput(phases, Confidence: 80, Rationale: "rationale");
    }
}

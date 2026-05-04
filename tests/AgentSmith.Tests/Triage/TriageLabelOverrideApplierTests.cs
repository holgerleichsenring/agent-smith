using AgentSmith.Application.Services.Triage;
using AgentSmith.Contracts.Models.Skills;
using FluentAssertions;

namespace AgentSmith.Tests.Triage;

public sealed class TriageLabelOverrideApplierTests
{
    private readonly TriageLabelOverrideApplier _sut = new();

    [Fact]
    public void Apply_TicketWithSkipLabel_OmitsSkill()
    {
        var output = TriageWith(PipelinePhase.Plan, lead: "architect", analysts: ["tester", "dba"]);
        var labels = new[] { "agent-smith:skip:dba" };

        var result = _sut.Apply(output, labels);

        result.Phases[PipelinePhase.Plan].Lead.Should().Be("architect");
        result.Phases[PipelinePhase.Plan].Analysts.Should().BeEquivalentTo("tester");
    }

    [Fact]
    public void Apply_NoTestAdaptionLabel_RemovesTesterFromAllRoles()
    {
        var output = new TriageOutput(
            new Dictionary<PipelinePhase, PhaseAssignment>
            {
                [PipelinePhase.Plan] = new("architect", new[] { "tester" }, Array.Empty<string>(), null),
                [PipelinePhase.Review] = new(null, Array.Empty<string>(), new[] { "tester", "reviewer-alpha" }, null)
            },
            85, string.Empty);
        var labels = new[] { "agent-smith:no-test-adaption" };

        var result = _sut.Apply(output, labels);

        result.Phases[PipelinePhase.Plan].Analysts.Should().BeEmpty();
        result.Phases[PipelinePhase.Review].Reviewers.Should().BeEquivalentTo("reviewer-alpha");
    }

    [Fact]
    public void Apply_NoLabelsMatch_OutputUnchanged()
    {
        var output = TriageWith(PipelinePhase.Plan, lead: "architect", analysts: ["tester"]);
        var labels = new[] { "bug", "frontend" };

        var result = _sut.Apply(output, labels);

        result.Should().BeSameAs(output);
    }

    private static TriageOutput TriageWith(PipelinePhase phase, string? lead, string[] analysts) =>
        new(new Dictionary<PipelinePhase, PhaseAssignment>
        {
            [phase] = new(lead, analysts, Array.Empty<string>(), null)
        }, 85, string.Empty);
}

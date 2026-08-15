using AgentSmith.Application.Services.Prompts;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Entities;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.Specs;

/// <summary>
/// p0413: the shape the scope classifier stated reaches the derivation — the one
/// call that decides how many phases a ticket becomes. The section states the
/// shape and the reason and nothing else: what the shape MEANS for the cut is a
/// rule of the derivation master, not a per-run instruction assembled in code.
/// A ticket with no stated shape must read the prompt it always read.
/// </summary>
public sealed class SpecShapePromptTests
{
    private const string Ticket = """
        Bring the declared set up to date in both components.
        """;

    [Fact]
    public void Derivation_DeterministicShape_ReachesThePromptWithItsReason()
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.WorkShape, new WorkShapeVerdict(
            WorkShape.Deterministic, "one declared set, applied the same way in both components"));

        var prompt = Compose(pipeline);

        prompt.Should().Contain("## The shape of this work");
        prompt.Should().Contain("Classified as: deterministic");
        prompt.Should().Contain("one declared set, applied the same way in both components");
    }

    [Fact]
    public void Derivation_NoShapeStated_LeavesThePromptUnchanged()
    {
        var withoutShape = Compose(new PipelineContext());

        withoutShape.Should().NotContain("The shape of this work");
    }

    [Fact]
    public void ShapeSection_MixedWithoutReason_StatesTheShapeAlone()
    {
        var rendered = WorkShapePromptSection.Render(new WorkShapeVerdict(WorkShape.Mixed));

        rendered.Should().Contain("Classified as: mixed");
        rendered.Should().NotContain("—");
    }

    [Fact]
    public void ShapeSection_NoVerdict_RendersNothing() =>
        WorkShapePromptSection.Render(null).Should().BeEmpty();

    private static string Compose(PipelineContext pipeline) =>
        SpecPromptComposer.Compose(
            new Ticket(new TicketId("42"), "Update the declared set", Ticket, null, "open", "test"),
            TicketSegmenter.Segment(Ticket), previous: null, cause: string.Empty, pipeline);
}

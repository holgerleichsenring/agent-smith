using AgentSmith.Application.Services;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Contracts.Commands;
using AgentSmith.Contracts.Models;
using AgentSmith.Domain.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-28-1da5e: the framework's own values for a ticket, rendered rather than restated.
/// <para>
/// The work-branch convention is used in five places in this source tree and appears in no skill
/// text anywhere. That is right for ACTING — the framework names branches and the model does not —
/// and wrong for reading the world back: asked whether a ticket's branches were finished, a
/// conversation had no way to know what to look for.
/// </para>
/// </summary>
public sealed class FrameworkFactsTests
{
    [Fact]
    public void FrameworkFacts_ABoundTicket_RendersTheBranchTheNamerWouldProduce()
    {
        var rendered = FrameworkFactsSection.Render(Pipeline(Facts()));

        // Not a convention to apply, but THE string the namer produces for this ticket — so the
        // prompt cannot drift from the code, because it is the code.
        rendered.Should().Contain(TicketBranchNamer.Compose(new TicketId("19400")).Value);
        rendered.Should().Contain("agent-smith/19400");
    }

    [Fact]
    public void FrameworkFacts_ARenamedLabelVocabulary_ReachesTheTurn()
    {
        // The labels became configurable per tracker, so an installation that renamed them had a
        // model reasoning about words nobody there uses.
        var rendered = FrameworkFactsSection.Render(Pipeline(
            new FrameworkFacts("agent-smith/19400", "wartet", "in arbeit", "spezifiziert")));

        rendered.Should().Contain("wartet").And.Contain("in arbeit").And.Contain("spezifiziert");
    }

    [Fact]
    public void FrameworkFacts_AnUnboundConversation_RendersNothing()
    {
        FrameworkFactsSection.Render(new PipelineContext()).Should().BeEmpty();
    }

    [Fact]
    public void FrameworkFacts_TheSection_StatesTheValuesRatherThanTheRule()
    {
        var rendered = FrameworkFactsSection.Render(Pipeline(Facts()));

        // A paragraph explaining HOW a branch name is built would be a second statement of
        // something the code decides, and the two would drift. Nothing here describes the
        // construction — no prefix, no slug, no platform segment.
        rendered.Should().NotContainAny("prefix", "slug", "platform", "hierarchical");
        rendered.Should().Contain("its OWN values for this ticket");
    }

    private static FrameworkFacts Facts() => new(
        TicketBranchNamer.Compose(new TicketId("19400")).Value,
        "agent-smith:enqueued", "agent-smith:in-progress", "agent-smith:approved-set");

    private static PipelineContext Pipeline(FrameworkFacts facts)
    {
        var pipeline = new PipelineContext();
        pipeline.Set(ContextKeys.SpecDialogFrameworkFacts, facts);
        return pipeline;
    }
}

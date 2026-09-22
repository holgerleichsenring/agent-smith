using AgentSmith.Application.Services.PhaseExecution;
using AgentSmith.Application.Services.SpecDialog;
using AgentSmith.Application.Services.Specs;
using AgentSmith.Application.Services.Validation;
using AgentSmith.Contracts.Models;
using FluentAssertions;

namespace AgentSmith.Tests.SpecDialog;

/// <summary>
/// 2026-09-13-b7ba: a filed ticket is a REQUIREMENT — what is wanted and why.
/// <para>
/// 2026-09-22-b3d7: the slice-record renderer these were written against is gone with the
/// records. The behaviour they pin is the REQUIREMENT BODY's, which the two surviving
/// renderings still have, so they are repointed at those rather than deleted with it: the
/// filed phase for the body's own shape, the cut's work ticket where a second renderer is
/// what makes the assertion worth making.
/// </para>
/// </summary>
public sealed class RequirementTicketTests
{
    private const string Yaml = """
        phase: p9000a
        goal: Widget storage layer
        scope:
          in: >
            The table, the migration and the repository that reads it.
          out: >
            THE API ON TOP. Its own slice, and it needs this one first.
        decisions:
          - key: |
              A TABLE BEFORE AN API, BECAUSE THE API SHAPE FOLLOWS THE STORAGE.
        steps:
          - id: the-table
            action: "Add the table and its migration."
        done:
          - "the table exists and the repository reads it"
        """;

    private static readonly PhaseDraft Draft =
        new("p9000a", "Widget storage layer", Yaml, ["p9000", "2026-09-17-042ea"]);

    [Fact]
    public void RequirementBody_HasNoFencedBlock() =>
        Render().Should().NotContain("```");

    [Fact]
    public void RequirementBody_RendersScopeInAndOut()
    {
        var body = Render();

        body.Should().Contain("## Scope");
        body.Should().Contain("The table, the migration");
        body.Should().Contain("THE API ON TOP",
            "what a slice deliberately excludes is half of what a requirement says");
    }

    [Fact]
    public void RequirementBody_OmitsSteps() =>
        Render().Should().NotContain("Add the table and its migration",
            "carrying the cut in prose would let the deriver reproduce the cut it must redo");

    [Fact]
    public void RenderBody_MapShapedDecisions_AppearInTheReasoning() =>
        Render().Should().Contain("A TABLE BEFORE AN API",
            "every modern decision is a {key: '…'} map, which used to render as an empty "
            + "line and be filtered away — so no filed ticket has ever carried its reasoning");

    /// <summary>2026-09-17-042eb: the done list is how the ticket says when it is finished.</summary>
    [Fact]
    public void RequirementTicket_ChildWithDone_RendersAcceptanceCriteria()
    {
        var body = Render();

        body.Should().Contain("## Acceptance criteria\n- the table exists and the repository reads it");
        AcceptanceCriteriaSection.Read(body).Should().Equal("the table exists and the repository reads it");
    }

    /// <summary>
    /// The edge checker only holds CHILDREN to siblings. A parent may require a phase outside the
    /// epic, and that precondition is still true of the whole cut.
    /// </summary>
    [Fact]
    public void RequirementTicket_EpicParent_KeepsAnOutsidePhaseIdRequirement()
    {
        var parent = new PhaseDraft("p9000", "Widget platform", "phase: p9000\ngoal: Widget platform", ["p8000"]);

        var body = new PhaseTicketRenderer().RenderEpicParent(parent, [Draft]).Body;

        body.Should().Contain("## Preconditions\n- p8000");
    }

    /// <summary>A block-scalar done item is one criterion; its continuation must not read back as a second.</summary>
    [Fact]
    public void RequirementTicket_MultiLineDone_ReadsBackAsOneCriterion()
    {
        const string yaml = "phase: p9000a\ngoal: Widget storage layer\ndone:\n  - |\n    the table exists\n    and the repository reads it\n";

        var body = new PhaseTicketRenderer()
            .RenderPhase(new PhaseDraft("p9000a", "Widget storage layer", yaml, [])).Body;

        AcceptanceCriteriaSection.Read(body).Should().Equal("the table exists and the repository reads it");
    }

    [Fact]
    public void RequirementTicket_FreeTextRequires_KeepsThePrecondition()
    {
        var draft = Draft with { Requires = ["the widget database exists in every environment"] };

        var body = new PhaseTicketRenderer().RenderPhase(draft).Body;

        body.Should().Contain("## Preconditions\n- the widget database exists in every environment",
            "no label carries a free-text precondition, so the body is the only place it lives");
    }

    /// <summary>
    /// 2026-09-17-0e79a: a filed phase is a REQUIREMENT with its acceptance criteria and its
    /// preconditions — no sibling set to subtract, so every requires: edge is a precondition.
    /// </summary>
    [Fact]
    public void FiledPhaseBody_IsTheRequirementShape()
    {
        var body = new PhaseTicketRenderer().RenderPhase(Draft).Body;

        body.Should().Contain(AcceptanceCriteriaSection.Heading);
        body.Should().Contain("## Preconditions\n- p9000");
        body.Should().NotContain("## Requires");
    }

    /// <summary>
    /// 2026-09-17-042ea: the tracker links the child to its parent and a label stamps it; a line in
    /// the body was a segment the deriver had to carry or discard, and nothing parsed it.
    /// </summary>
    [Fact]
    public void RequirementTicket_Body_CarriesNoParentLine() =>
        Render().Should().NotContain("Parent:");

    /// <summary>
    /// 2026-09-22-b3d7: the cut's work ticket is the second renderer that carries the requirement
    /// body, and it is the one a run actually picks up — so the absent-not-malformed reading is
    /// pinned on it as well as on the filed phase.
    /// </summary>
    [Fact]
    public void SpecSource_TheCutsWorkTicket_LeavesTheDerivationToRun() =>
        new PhaseSpecFromTicket(
                new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader())
            .Extract(new PhaseTicketRenderer().RenderEpicParent(Draft, []).Body)
            .Should().BeOfType<PhaseSpecInvalid>()
            .Which.IsAbsent.Should().BeTrue();

    /// <summary>
    /// 2026-09-17-0e79a: the approved set is stored and carried, not embedded. A fence in the body
    /// would be a second truth — one anyone with tracker access can edit, and one the source
    /// precedence would take over the record a person actually approved.
    /// </summary>
    [Fact]
    public void FiledPhaseBody_CarriesNoYamlFence()
    {
        var body = new PhaseTicketRenderer().RenderPhase(Draft, "session-7").Body;

        body.Should().NotContain("```");
        body.Should().Contain(PhaseTicketRenderer.SpecificationHeading)
            .And.Contain("session-7", "the body points at the conversation the set was approved in");
    }

    /// <summary>
    /// 2026-09-17-0e79a: the filed ticket carries the stamp that holds it to "the approved set
    /// must have reached the run" — and since 2026-09-22-766b it is the only framework label it
    /// carries. A hand-written ticket carries none of them and its spec legitimately lives in its
    /// description.
    /// </summary>
    [Fact]
    public void FiledPhase_Labels_CarryTheApprovedSetStamp()
    {
        FiledTicketLabels.CarriesApprovedSet(
            [FiledTicketLabels.ApprovedSetStamp, "bug"]).Should().BeTrue();
        FiledTicketLabels.CarriesApprovedSet(["phase"]).Should().BeFalse(
            "a hand-written phase ticket is not held to a set nobody approved");
    }

    /// <summary>The extractor must read a filed body as ABSENT, never as malformed.</summary>
    [Fact]
    public void FiledPhaseBody_ReadsBackAsNoSpecAtAll() =>
        new PhaseSpecFromTicket(
                new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader())
            .Extract(new PhaseTicketRenderer().RenderPhase(Draft).Body)
            .Should().BeOfType<PhaseSpecInvalid>()
            .Which.IsAbsent.Should().BeTrue();

    [Fact]
    public void SpecSource_RequirementTicket_LeavesTheDerivationToRun()
    {
        var extraction = new PhaseSpecFromTicket(
            new SpecDraftValidator(new PhaseSpecSchemaProvider()), new PhaseDraftReader())
            .Extract(Render());

        extraction.Should().BeOfType<PhaseSpecInvalid>()
            .Which.IsAbsent.Should().BeTrue(
                "absent — not malformed — is what makes SpecSourceResolver fall through to "
                + "Derived instead of failing the run");
    }

    private static string Render() => new PhaseTicketRenderer().RenderPhase(Draft).Body;
}
